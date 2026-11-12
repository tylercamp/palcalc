using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Support.Level;
using PalCalc.SaveReader.SaveFile.Virtual;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.Persistence.Serialization;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Inspector.Details;

namespace PalCalc.UI.Tests;

[TestClass]
public class PalInstanceIdentityTests
{
    [TestMethod]
    public void WorldReaderAndInspectorKeepBothPalsWithTheSameBareGuid()
    {
        var db = PalDB.LoadEmbedded();
        var instanceId = Guid.NewGuid();
        var original = new PalInstanceId(Guid.Empty, instanceId);
        var imported = new PalInstanceId(Guid.NewGuid(), instanceId);
        var owner = Guid.NewGuid();
        var containerId = Guid.NewGuid();
        var container = new RawPalContainerContents
        {
            Id = containerId.ToString(),
            MaxEntries = 30,
            Slots =
            [
                new() { PalInstanceId = original, SlotIndex = 1 },
                new() { PalInstanceId = imported, SlotIndex = 3 },
            ],
        };
        var raw = new RawLevelSaveData
        {
            Characters = [Character(original), Character(imported)],
            ContainerContents = [container],
            Groups = [],
            Bases = [],
            MapObjects = [],
        };

        var loaded = new TestLevel(raw).ReadCharacterData(db, GameSettings.Defaults, [], null);
        Assert.HasCount(2, loaded.Pals);
        Assert.IsTrue(loaded.Pals.All(p => p.Location.Type == LocationType.Palbox));
        Assert.AreEqual(1, loaded.Pals.Single(p => p.PalInstanceId == original).Location.Index);
        Assert.AreEqual(3, loaded.Pals.Single(p => p.PalInstanceId == imported).Location.Index);
        Assert.AreNotEqual(loaded.Pals[0], loaded.Pals[1]);
        var global = new TestGlobal(Character(imported)).ReadPals("GLOBAL_PAL_STORAGE").Pals.Single();
        Assert.AreEqual<PalInstanceId?>(imported, global.PalInstanceId);
        Assert.AreNotEqual(loaded.Pals.Single(p => p.PalInstanceId == imported), global);

        var inspector = new InspectedContainerDetailsViewModel("Palbox", OwnerViewModel.Unknown,
            loaded.Pals, raw.Characters, container, LocationType.Palbox, null);
        var slot = (PalContainerSlotDetailsViewModel)inspector.Slots[3];
        Assert.AreEqual<PalInstanceId?>(imported, slot.Instance.PalInstanceId);
        Assert.AreEqual(imported, slot.RawInstance.PalInstanceId);

        var unresolved = new InspectedContainerDetailsViewModel("Palbox", OwnerViewModel.Unknown,
            [], [], container, LocationType.Palbox, null);
        Assert.AreEqual($"{original.PlayerUId:D} / {instanceId:D}",
            ((PalContainerSlotDetailsViewModel)unresolved.Slots[1]).DisplayName.Value);
        Assert.AreEqual($"{imported.PlayerUId:D} / {instanceId:D}",
            ((PalContainerSlotDetailsViewModel)unresolved.Slots[3]).DisplayName.Value);

        raw.Characters.Add(Character(original));
        loaded = new TestLevel(raw).ReadCharacterData(db, GameSettings.Defaults, [], null);
        Assert.AreEqual<PalInstanceId?>(imported, loaded.Pals.Single().PalInstanceId);

        GvasCharacterInstance Character(PalInstanceId id) => new()
        {
            PalInstanceId = id,
            CharacterId = db.Pals.First().InternalName,
            OwnerPlayerId = owner,
            ContainerId = containerId,
            PassiveSkills = [],
            ActiveSkills = [],
            EquippedActiveSkills = [],
        };
    }

    [TestMethod]
    public void InspectorInfersNativeContainerOwnerOnlyFromPalsConfirmedByRawSlots()
    {
        var db = PalDB.LoadEmbedded();
        var containerId = Guid.NewGuid();
        var currentContainerId = Guid.NewGuid();
        var currentId = new PalInstanceId(Guid.Empty, Guid.NewGuid());
        var current = new GvasCharacterInstance
        {
            PalInstanceId = currentId,
            ContainerId = currentContainerId,
            CharacterId = db.Pals.First().InternalName,
        };
        var raw = new RawLevelSaveData
        {
            Characters = [current],
            ContainerContents = [Container(containerId), Container(currentContainerId)],
            Groups = [],
            Bases = [],
            MapObjects = [],
        };
        using var save = new VirtualSaveGame("player", "world", new InspectorLevel(raw), null, null, null);
        var converter = new PalInstanceJsonConverter(db);
        var cached = CachedSaveGame.FromJson(JsonConvert.SerializeObject(new
        {
            OwnedPals = new[]
            {
                MakePal(containerId, new PalInstanceId(Guid.Empty, Guid.NewGuid())),
                MakePal(currentContainerId, currentId),
            },
        }, converter), db);
        cached.UnderlyingSave = save;

        var inspector = new SaveDetailsViewModel(null, cached);
        var staleContainer = inspector.Containers.Single(c => c.Id == containerId.ToString());
        Assert.AreEqual(OwnerViewModel.Unknown.Id, staleContainer.Owner.Id);
        var staleSlot = (PalContainerSlotDetailsViewModel)staleContainer.Slots[0];
        Assert.IsNull(staleSlot.Instance);
        Assert.AreSame(current, staleSlot.RawInstance);
        var currentContainer = inspector.Containers.Single(c => c.Id == currentContainerId.ToString());
        Assert.AreEqual("owner", currentContainer.Owner.Id);
        Assert.IsNotNull(((PalContainerSlotDetailsViewModel)currentContainer.Slots[0]).Instance);

        RawPalContainerContents Container(Guid id) => new()
        {
            Id = id.ToString(),
            MaxEntries = 1,
            Slots = [new() { PalInstanceId = currentId, SlotIndex = 0 }],
        };

        PalInstance MakePal(Guid container, PalInstanceId key) => new()
        {
            Pal = db.Pals.First(),
            PalInstanceId = key,
            OwnerPlayerId = "owner",
            Location = new PalLocation { Type = LocationType.Base, ContainerId = container.ToString(), Index = 0 },
            PassiveSkills = [],
        };
    }

    [TestMethod]
    public void SnapshotsPreserveKnownIdsAndKeepLegacyIdsUnknown()
    {
        var db = PalDB.LoadEmbedded();
        var key = new PalInstanceId(Guid.NewGuid(), Guid.NewGuid());
        var world = MakePal(null);
        var global = MakePal("GLOBAL_PAL_STORAGE");
        Assert.AreNotEqual(world, global);
        Assert.AreNotEqual(new PalInstance(), new PalInstance());

        var converter = new PalInstanceJsonConverter(db);
        var cacheJson = JObject.Parse(JsonConvert.SerializeObject(global, converter));
        Assert.IsNull(cacheJson["InstanceId"]);
        var cached = JsonConvert.DeserializeObject<PalInstance>(cacheJson.ToString(), converter);
        Assert.IsNotNull(cached);
        Assert.AreEqual(global, cached);
        cached.Location.Index = 8;
        Assert.AreEqual(global.GetHashCode(), cached.GetHashCode());
        cacheJson.Remove(nameof(PalInstance.PalInstanceId));
        cacheJson.Remove(nameof(PalInstance.ExternalStorageContainerId));
        cacheJson["InstanceId"] = key.InstanceId.ToString();
        Assert.IsNull(JsonConvert.DeserializeObject<PalInstance>(cacheJson.ToString(), converter)!.PalInstanceId);

        var snapshot = JObject.FromObject(CustomizationsJsonSerializer.ToDto(global));
        Assert.IsNull(snapshot["InstanceId"]);
        var restored = CustomizationsJsonSerializer.FromDto(snapshot.ToObject<PalInstanceSnapshotDto>()!, db);
        Assert.AreEqual(global, restored);

        Assert.IsTrue(snapshot.Remove(nameof(PalInstance.PalInstanceId)));
        Assert.IsTrue(snapshot.Remove(nameof(PalInstance.ExternalStorageContainerId)));
        snapshot["InstanceId"] = key.InstanceId.ToString();
        var legacy = CustomizationsJsonSerializer.FromDto(snapshot.ToObject<PalInstanceSnapshotDto>()!, db);
        Assert.IsNull(legacy.PalInstanceId);

        PalInstance MakePal(string? storageId) => new()
        {
            Pal = db.Pals.First(),
            Location = new PalLocation { Type = storageId == null ? LocationType.Palbox : LocationType.GlobalPalStorage },
            Gender = PalGender.FEMALE,
            PassiveSkills = [],
            PalInstanceId = key,
            ExternalStorageContainerId = storageId,
        };
    }

    private sealed class TestLevel(RawLevelSaveData raw) : LevelSaveFile(null)
    {
        public override RawLevelSaveData ReadRawCharacterData() => raw;
    }

    private sealed class InspectorLevel(RawLevelSaveData raw) : VirtualLevelSaveFile
    {
        public override RawLevelSaveData ReadRawCharacterData() => raw;
    }

    private sealed class TestGlobal(GvasCharacterInstance raw) : GlobalPalStorageSaveFile(null)
    {
        public override List<GvasCharacterInstance> ReadRawCharacters() => [raw];
    }
}
