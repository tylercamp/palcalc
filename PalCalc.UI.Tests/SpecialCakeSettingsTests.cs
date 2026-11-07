using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.Persistence.Serialization;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class SpecialCakeSettingsTests
{
    [TestMethod]
    public void PassiveCakeUseIsDisabledByDefault()
    {
        var controls = new SolverControlsViewModel(null, null, null, null, null);
        Assert.IsFalse(controls.AllowSpecialCakesForPassives);
        Assert.IsFalse(new SerializableSolverSettings().AllowSpecialCakesForPassives);
        Assert.AreEqual(SpecialCakePolicy.AttackInheritanceOnly,
            controls.ConfiguredSolverSettings(new GameSettings(), []).SpecialCakePolicy);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CheckboxPolicySurvivesApplicationSettingsAndResultSnapshots(bool enabled)
    {
        var controls = new SolverControlsViewModel(null, null, null, null, null)
        {
            AllowSpecialCakesForPassives = enabled,
        };
        var appSettings = new AppSettings { SolverSettings = controls.AsModel };
        var restored = AppSettingsJsonSerializer.FromDto(AppSettingsJsonSerializer.FromCurrentJson(
            AppSettingsJsonSerializer.ToJson(AppSettingsJsonSerializer.ToDto(appSettings))));
        Assert.AreEqual(enabled, restored.SolverSettings.AllowSpecialCakesForPassives);

        var snapshot = JObject.FromObject(ResultJsonSerializer.ToDto(controls.AsModel))
            .ToObject<SolverSettingsSnapshotDto>()!;
        var restoredSnapshot = ResultJsonSerializer.FromDto(snapshot, PalDB.LoadEmbedded());
        Assert.AreEqual(enabled, restoredSnapshot.AllowSpecialCakesForPassives);
        var restoredControls = new SolverControlsViewModel(null, null, null, null, null)
        {
            AllowSpecialCakesForPassives = !enabled,
        };
        restoredControls.CopyFrom(restoredSnapshot);
        Assert.AreEqual(enabled, restoredControls.AllowSpecialCakesForPassives);
        Assert.AreEqual(enabled ? SpecialCakePolicy.AttacksAndPassives : SpecialCakePolicy.AttackInheritanceOnly,
            restoredControls.ConfiguredSolverSettings(new GameSettings(), []).SpecialCakePolicy);
    }
}
