using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
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

    private static JsonSerializerSettings Settings(PalDB db) => new()
    {
        Converters = { new PalSpecifierViewModelConverter(db, new GameSettings(), null) }
    };
}
