using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class PalSpecifierAttackTests
{
    [TestMethod]
    public void AttackSlotsProduceRequiredAttacks()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(2).ToList();
        var viewModel = new PalSpecifierViewModel(
            "target",
            new PalSpecifier
            {
                Pal = db.Pals.First(),
            }
        );

        viewModel.RequiredAttacks.Attack2 = ActiveSkillViewModel.Make(attacks[1]);

        Assert.IsTrue(viewModel.RequiredAttacks.HasItems);
        Assert.AreEqual(attacks[1].InternalName, viewModel.ModelObject.RequiredAttacks.Single().InternalName);

        viewModel.RequiredAttacks.Attack2 = null!;

        Assert.IsFalse(viewModel.RequiredAttacks.HasItems);
        Assert.IsEmpty(viewModel.ModelObject.RequiredAttacks);
    }

    [TestMethod]
    public void RequiredAttacksRoundTripThroughTargetJson()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(PalSpecifier.MaxRequiredAttacks).ToList();
        var target = new PalSpecifier
        {
            Pal = db.Pals.First(),
            RequiredAttacks = attacks,
        };
        var viewModel = new PalSpecifierViewModel("target", target);

        var json = JsonConvert.SerializeObject(viewModel, WriteSettings(db));
        var reloaded = JsonConvert.DeserializeObject<PalSpecifierViewModel>(json, ReadSettings(db));

        Assert.IsNotNull(reloaded);
        CollectionAssert.AreEqual(
            attacks.Select(attack => attack.InternalName).ToArray(),
            reloaded.RequiredAttacks.AsEnumerable().Select(attack => attack.ModelObject.InternalName).ToArray()
        );
        CollectionAssert.AreEqual(
            attacks.Select(attack => attack.InternalName).ToArray(),
            reloaded.ModelObject.RequiredAttacks.Select(attack => attack.InternalName).ToArray()
        );
        CollectionAssert.AreEqual(
            attacks.Select(attack => attack.InternalName).ToArray(),
            viewModel.Copy().ModelObject.RequiredAttacks.Select(attack => attack.InternalName).ToArray()
        );
    }

    [TestMethod]
    public void MissingRequiredAttacksLoadAsEmpty()
    {
        var db = PalDB.LoadEmbedded();
        var target = new PalSpecifier
        {
            Pal = db.Pals.First(),
            RequiredAttacks = [db.ActiveSkills.First()],
        };
        var viewModel = new PalSpecifierViewModel("target", target);
        var json = JObject.Parse(JsonConvert.SerializeObject(viewModel, WriteSettings(db)));
        json.Remove("RequiredAttacks");

        var reloaded = JsonConvert.DeserializeObject<PalSpecifierViewModel>(json.ToString(), ReadSettings(db));

        Assert.IsNotNull(reloaded);
        Assert.IsFalse(reloaded.RequiredAttacks.HasItems);
        Assert.IsEmpty(reloaded.ModelObject.RequiredAttacks);
    }

    [TestMethod]
    public void NullGapsAndDuplicateSlotsNormalizeForTheModel()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(2).ToArray();
        var viewModel = new PalSpecifierViewModel("target", new PalSpecifier { Pal = db.Pals.First() });

        viewModel.RequiredAttacks.Attack1 = ActiveSkillViewModel.Make(attacks[0]);
        viewModel.RequiredAttacks.Attack3 = ActiveSkillViewModel.Make(attacks[1]);
        viewModel.RequiredAttacks.Attack6 = ActiveSkillViewModel.Make(attacks[0]);

        CollectionAssert.AreEqual(
            attacks.Select(attack => attack.InternalName).ToArray(),
            viewModel.ModelObject.RequiredAttacks.Select(attack => attack.InternalName).ToArray()
        );

        var copy = viewModel.Copy();
        Assert.AreEqual(viewModel.RequiredAttacks.Attack1, copy.RequiredAttacks.Attack1);
        Assert.IsNull(copy.RequiredAttacks.Attack2);
        Assert.AreEqual(viewModel.RequiredAttacks.Attack3, copy.RequiredAttacks.Attack3);
        Assert.AreEqual(viewModel.RequiredAttacks.Attack6, copy.RequiredAttacks.Attack6);
    }

    [TestMethod]
    public void SingleAttackTargetJsonPopulatesTheFirstSlot()
    {
        var db = PalDB.LoadEmbedded();
        var attack = db.ActiveSkills.First();
        var json = JObject.Parse(JsonConvert.SerializeObject(
            new PalSpecifierViewModel("target", new PalSpecifier { Pal = db.Pals.First(), RequiredAttacks = [attack] }),
            WriteSettings(db)
        ));

        var reloaded = JsonConvert.DeserializeObject<PalSpecifierViewModel>(json.ToString(), ReadSettings(db));

        Assert.AreEqual(attack.InternalName, reloaded!.RequiredAttacks.Attack1!.ModelObject.InternalName);
        Assert.IsNull(reloaded.RequiredAttacks.Attack2);
    }

    [TestMethod]
    public void ImportedTargetsWithMoreThanSixAttacksAreRejected()
    {
        var db = PalDB.LoadEmbedded();
        var json = JObject.Parse(JsonConvert.SerializeObject(
            new PalSpecifierViewModel("target", new PalSpecifier { Pal = db.Pals.First() }),
            WriteSettings(db)
        ));
        json["RequiredAttacks"] = new JArray(db.ActiveSkills.Take(PalSpecifier.MaxRequiredAttacks + 1).Select(attack => attack.InternalName));

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<PalSpecifierViewModel>(json.ToString(), ReadSettings(db))
        );
    }

    [TestMethod]
    public void LegacySettingsKeepSpecialCakesDisabled()
    {
        var legacySettings = JsonConvert.DeserializeObject<SerializableSolverSettings>("{ \"MaxBreedingSteps\": 3 }");
        var controls = new SolverControlsViewModel(null, null, null, null, null);

        controls.CopyFrom(legacySettings!);

        Assert.AreEqual(0, controls.ConfiguredSolverSettings(new GameSettings(), []).MaxSpecialCakes);
    }

    [TestMethod]
    public void RandomAttackRoundTripsThroughActiveSkillConverter()
    {
        var db = PalDB.LoadEmbedded();
        var random = new RandomActiveSkill();
        var settings = new JsonSerializerSettings
        {
            Converters = { new ActiveSkillConverter(db, new GameSettings()) }
        };

        var json = JsonConvert.SerializeObject(new { Skill = (ActiveSkill)random }, settings);
        var reloaded = JObject.Parse(json)["Skill"]!.ToObject<ActiveSkill>(JsonSerializer.Create(settings));

        Assert.IsInstanceOfType<RandomActiveSkill>(reloaded);

        var viewModelSettings = new JsonSerializerSettings
        {
            Converters = { new ActiveSkillViewModelConverter(db, new GameSettings()) }
        };
        var viewModelJson = JsonConvert.SerializeObject(new { Skill = ActiveSkillViewModel.Make(random) }, viewModelSettings);
        var viewModelReloaded = JObject.Parse(viewModelJson)["Skill"]!.ToObject<ActiveSkillViewModel>(JsonSerializer.Create(viewModelSettings));

        Assert.IsInstanceOfType<RandomActiveSkill>(viewModelReloaded!.ModelObject);
    }

    [TestMethod]
    public void BredReferenceStateRoundTrips()
    {
        var db = PalDB.LoadEmbedded();
        var gameSettings = new GameSettings();
        var attack = db.ActiveSkills.First();
        var parent1 = Owned(db.Pals.First(), "parent-1", attack, 1);
        var parent2 = Owned(db.Pals.First(), "parent-2", attack, 2);
        var original = new BredPalReference(
            gameSettings,
            db.Pals.First(),
            parent1,
            parent2,
            new List<PassiveSkill>(),
            passivesProbability: 0.5f,
            new IV_Set
            {
                HP = new IV_Value(true, 10, 20),
                Attack = IV_Value.Random,
                Defense = IV_Value.Random,
            },
            ivsProbability: 0.75f,
            attackProfile: AttackProfile.Inactive,
            materializedAttackInheritance: null,
            avgRequiredBreedings: null,
            gender: PalGender.WILDCARD
        );

        var settings = new JsonSerializerSettings
        {
            Converters =
            {
                new PalReferenceConverter(db, gameSettings, new SerializableSolverSettings(), new PalSpecifier { Pal = db.Pals.First() })
            }
        };

        var json = JsonConvert.SerializeObject(new { Ref = (IPalReference)original }, settings);
        var restored = JObject.Parse(json)["Ref"]!.ToObject<IPalReference>(JsonSerializer.Create(settings))!;

        var restoredBred = (BredPalReference)restored;
        Assert.AreEqual(original.AvgRequiredBreedings, restoredBred.AvgRequiredBreedings);
    }

    [TestMethod]
    public void MaterializedParentLoadoutsFollowNormalizedParentOrder()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(2).ToArray();
        var pals = db.Pals.OrderBy(pal => pal.InternalIndex).ToArray();
        var firstParent = Owned(pals[0], "parent-1", attacks[0], 1);
        var secondParent = Owned(pals[^1], "parent-2", attacks[1], 2);
        var inheritance = new MaterializedAttackInheritance(
            AttackInheritanceMode.Normal,
            [attacks[0]],
            [attacks[1]],
            [],
            [],
            SpecialCakes: 0,
            AttackProbability: 1
        );
        var bred = new BredPalReference(
            new GameSettings(),
            pals[0],
            firstParent,
            secondParent,
            [],
            passivesProbability: 1,
            new IV_Set { HP = IV_Value.Random, Attack = IV_Value.Random, Defense = IV_Value.Random },
            ivsProbability: 1,
            attackProfile: AttackProfile.Inactive,
            materializedAttackInheritance: inheritance,
            avgRequiredBreedings: 1,
            gender: PalGender.WILDCARD
        );

        Assert.AreSame(secondParent, bred.Parent1);
        Assert.AreSame(firstParent, bred.Parent2);
        Assert.AreEqual(attacks[1], bred.MaterializedAttackInheritance!.Parent1Loadout.Single());
        Assert.AreEqual(attacks[0], bred.MaterializedAttackInheritance.Parent2Loadout.Single());
    }

    [TestMethod]
    public void NoAttackResultDisplayKeepsCakeCountAsTotalEggs()
    {
        var db = PalDB.LoadEmbedded();
        var attack = db.ActiveSkills.First();
        var result = new BredPalReference(
            new GameSettings(),
            db.Pals.First(),
            Owned(db.Pals.First(), "parent-1", attack, 1),
            Owned(db.Pals.First(), "parent-2", attack, 2),
            [],
            passivesProbability: 1,
            new IV_Set { HP = IV_Value.Random, Attack = IV_Value.Random, Defense = IV_Value.Random },
            ivsProbability: 1,
            attackProfile: AttackProfile.Inactive,
            materializedAttackInheritance: null,
            avgRequiredBreedings: null,
            gender: PalGender.WILDCARD
        );

        var display = new BreedingResultViewModel(null, new GameSettings(), result, []);

        Assert.IsFalse(display.EffectiveAttacks.HasItems);
        Assert.AreEqual(result.NumTotalEggs, display.NumEggs);
    }

    private static OwnedPalReference Owned(Pal pal, string instanceId, ActiveSkill attack, int index) =>
        new(
            new PalInstance
            {
                InstanceId = instanceId,
                Pal = pal,
                Gender = PalGender.WILDCARD,
                PassiveSkills = new List<PassiveSkill>(),
                ActiveSkills = new List<ActiveSkill> { attack },
                EquippedActiveSkills = new List<ActiveSkill> { attack },
                Location = new PalLocation { Type = LocationType.Palbox, Index = index },
            },
            new List<PassiveSkill>(),
            new IV_Set
            {
                HP = IV_Value.Random,
                Attack = IV_Value.Random,
                Defense = IV_Value.Random,
            },
            attackProfile: AttackProfile.Inactive
        );

    private static JsonSerializerSettings ReadSettings(PalDB db) => new()
    {
        Converters = { new PalSpecifierViewModelReader(db, new GameSettings(), new CachedSaveGame(null)) }
    };

    private static JsonSerializerSettings WriteSettings(PalDB db) => new()
    {
        Converters = { new PalSpecifierViewModelWriter(db, new GameSettings()) }
    };
}
