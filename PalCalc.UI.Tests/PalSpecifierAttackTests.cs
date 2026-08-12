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
    public void RequiredAttackRoundTripsThroughTargetJson()
    {
        var db = PalDB.LoadEmbedded();
        var attack = db.ActiveSkills.First();
        var target = new PalSpecifier
        {
            Pal = db.Pals.First(),
            RequiredAttack = attack,
        };
        var viewModel = new PalSpecifierViewModel("target", target);

        var json = JsonConvert.SerializeObject(viewModel, Settings(db));
        var reloaded = JsonConvert.DeserializeObject<PalSpecifierViewModel>(json, Settings(db));

        Assert.IsNotNull(reloaded);
        Assert.AreEqual(attack.InternalName, reloaded.RequiredAttack.ModelObject.InternalName);
        Assert.AreEqual(attack.InternalName, reloaded.ModelObject.RequiredAttack.InternalName);
        Assert.AreEqual(attack.InternalName, viewModel.Copy().ModelObject.RequiredAttack.InternalName);
    }

    [TestMethod]
    public void MissingRequiredAttackLoadsAsNull()
    {
        var db = PalDB.LoadEmbedded();
        var target = new PalSpecifier
        {
            Pal = db.Pals.First(),
            RequiredAttack = db.ActiveSkills.First(),
        };
        var viewModel = new PalSpecifierViewModel("target", target);
        var json = JObject.Parse(JsonConvert.SerializeObject(viewModel, Settings(db)));
        json.Remove("RequiredAttack");

        var reloaded = JsonConvert.DeserializeObject<PalSpecifierViewModel>(json.ToString(), Settings(db));

        Assert.IsNotNull(reloaded);
        Assert.IsNull(reloaded.RequiredAttack);
        Assert.IsNull(reloaded.ModelObject.RequiredAttack);
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

    private static JsonSerializerSettings Settings(PalDB db) => new()
    {
        Converters = { new PalSpecifierViewModelConverter(db, new GameSettings(), null) }
    };
}
