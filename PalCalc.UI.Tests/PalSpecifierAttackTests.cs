using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;

namespace PalCalc.UI.Tests;

[TestClass]
public class PalSpecifierAttackTests
{
    [TestMethod]
    public void RequiredAttackSelectorUpdatesCollection()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(2).ToList();
        var viewModel = new PalSpecifierViewModel(
            "target",
            new PalSpecifier
            {
                Pal = db.Pals.First(),
                RequiredAttacks = [attacks[0]],
            }
        );

        viewModel.RequiredAttack = ActiveSkillViewModel.Make(attacks[1]);

        Assert.IsTrue(viewModel.RequiredAttacks.HasItems);
        Assert.AreEqual(attacks[1].InternalName, viewModel.RequiredAttacks.AsModelEnumerable().Single().InternalName);

        viewModel.RequiredAttack = null;

        Assert.IsFalse(viewModel.RequiredAttacks.HasItems);
    }

    [TestMethod]
    public void RequiredAttacksRoundTripThroughTargetJson()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(2).ToList();
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
            reloaded.RequiredAttacks.Attacks.Select(attack => attack.ModelObject.InternalName).ToArray()
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
        Assert.IsNull(reloaded.RequiredAttack);
        Assert.IsEmpty(reloaded.ModelObject.RequiredAttacks);
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
    public void BredReferenceAttackStateRoundTrips()
    {
        var db = PalDB.LoadEmbedded();
        var gameSettings = new GameSettings();
        var attack = db.ActiveSkills.First();
        var random = new RandomActiveSkill();
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
            actualAttack: attack,
            effectiveAttack: random,
            attacksProbability: 0.25f
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
        Assert.AreEqual(attack.InternalName, restoredBred.ActualAttack.InternalName);
        Assert.IsInstanceOfType<RandomActiveSkill>(restoredBred.EffectiveAttack);
        Assert.AreEqual(original.AttacksProbability, restoredBred.AttacksProbability);
        Assert.AreEqual(original.AvgRequiredBreedings, restoredBred.AvgRequiredBreedings);

        var oldJson = JObject.Parse(json);
        foreach (var obj in oldJson.DescendantsAndSelf().OfType<JObject>())
        {
            obj.Remove("ActualAttack");
            obj.Remove("EffectiveAttack");
            obj.Remove("AttacksProbability");
        }

        var oldResult = (BredPalReference)oldJson["Ref"]!.ToObject<IPalReference>(JsonSerializer.Create(settings))!;
        Assert.IsNull(oldResult.ActualAttack);
        Assert.IsNull(oldResult.EffectiveAttack);
        Assert.AreEqual(1.0f, oldResult.AttacksProbability);
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
            actualAttack: attack,
            effectiveAttack: attack
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
