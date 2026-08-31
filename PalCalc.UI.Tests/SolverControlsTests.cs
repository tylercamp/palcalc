using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class SolverControlsTests
{
    [TestMethod]
    public void LegacySettingsDefaultSpecialCakesToZero()
    {
        var settings = JsonConvert.DeserializeObject<SerializableSolverSettings>("{ \"MaxBreedingSteps\": 3 }");

        Assert.AreEqual(0, settings!.MaxSpecialCakes);
    }

    [TestMethod]
    public void SpecialCakeLimitClampsAndRoundTripsThroughSettings()
    {
        var controls = Controls();
        Assert.AreEqual(0, controls.MaxSpecialCakes);

        controls.MaxSpecialCakes = -1;
        Assert.AreEqual(0, controls.MaxSpecialCakes);

        controls.MaxSpecialCakes = 42;
        var settings = controls.AsModel;
        var reloaded = Controls();
        reloaded.CopyFrom(settings);

        Assert.AreEqual(42, settings.MaxSpecialCakes);
        Assert.AreEqual(42, reloaded.MaxSpecialCakes);
    }

    [TestMethod]
    public void ConfiguredSolverSettingsPassesTheExactSpecialCakeLimit()
    {
        var controls = Controls();
        controls.MaxSpecialCakes = 42;

        Assert.AreEqual(42, controls.ConfiguredSolverSettings(new GameSettings(), []).MaxSpecialCakes);
    }

    [TestMethod]
    public void SpecialCakeLimitControlsCakeDependentRoutes()
    {
        var db = PalDB.LoadEmbedded();
        var targetPal = "Wixen Noct".ToPal(db);
        var attacks = db.ActiveSkills
            .Where(attack => attack.CanInherit && !targetPal.Level1ActiveSkills(db).Contains(attack))
            .Take(2)
            .ToArray();
        var parents = new List<PalInstance>
        {
            Owned("Katress", PalGender.MALE, attacks[0], 1),
            Owned("Wixen", PalGender.FEMALE, attacks[1], 2),
        };
        var target = new PalSpecifier { Pal = targetPal, RequiredAttacks = attacks.ToList() };
        var controls = Controls();

        controls.MaxSpecialCakes = 0;
        Assert.IsEmpty(Solve(controls, target, parents));

        controls.MaxSpecialCakes = 100;
        Assert.IsNotEmpty(Solve(controls, target, parents));
    }

    private static SolverControlsViewModel Controls()
    {
        var controls = new SolverControlsViewModel(null, null, null, null, null)
        {
            MaxBreedingSteps = 1,
            MaxSolverIterations = 1,
            MaxWildPals = 0,
        };
        return controls;
    }

    private static IReadOnlyCollection<PalCalc.Solver.PalReference.IPalReference> Solve(
        SolverControlsViewModel controls,
        PalSpecifier target,
        List<PalInstance> parents
    ) => new BreedingSolver()
        .Solve(
            new BreedingSolverRequest(target, controls.ConfiguredSolverSettings(new GameSettings(), parents)),
            new SolverStateController()
        )
        .Results;

    private static PalInstance Owned(string palName, PalGender gender, ActiveSkill attack, int index)
    {
        var db = PalDB.LoadEmbedded();
        return new PalInstance
        {
            InstanceId = $"special-cake-{index}",
            OwnerPlayerId = "special-cake-test",
            Pal = palName.ToPal(db),
            Gender = gender,
            PassiveSkills = [],
            ActiveSkills = [attack],
            EquippedActiveSkills = [attack],
            Location = new PalLocation { ContainerId = "special-cake-test", Type = LocationType.Palbox, Index = index },
        };
    }
}
