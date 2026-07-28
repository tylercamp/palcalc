using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class InitialPalBuilderTests
{
    [TestMethod]
    public void Build_CombinesEquivalentOppositeGenderOwnedPals()
    {
        var male = SolverTestScenario.Owned(
            "Katress",
            PalGender.MALE
        );
        var female = SolverTestScenario.Owned(
            "Katress",
            PalGender.FEMALE
        );
        var configuredSolver = SolverTestScenario.Solver(
            [male, female],
            maxBreedingSteps: 1
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB
        ).Build(Target());

        Assert.AreEqual(1, seeds.Count);
        var composite =
            seeds.Single() as CompositeOwnedPalReference;
        Assert.IsNotNull(composite);
        Assert.AreSame(male, composite.Male.UnderlyingInstance);
        Assert.AreSame(female, composite.Female.UnderlyingInstance);
    }

    [TestMethod]
    public void Build_SelectsOwnedInstanceWithFewerIrrelevantPassives()
    {
        var irrelevant =
            "Swift".ToStandardPassive(SolverTestScenario.DB);
        var clean = SolverTestScenario.Owned(
            "Katress",
            PalGender.MALE
        );
        var noisy = SolverTestScenario.Owned(
            "Katress",
            PalGender.MALE,
            passives: [irrelevant]
        );
        var configuredSolver = SolverTestScenario.Solver(
            [noisy, clean],
            maxBreedingSteps: 1
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB
        ).Build(Target());

        Assert.AreEqual(1, seeds.Count);
        var selected = seeds.Single() as OwnedPalReference;
        Assert.IsNotNull(selected);
        Assert.AreSame(clean, selected.UnderlyingInstance);
    }

    [TestMethod]
    public void Build_AddsConfiguredWildPassiveCountVariants()
    {
        var katress = "Katress".ToPal(SolverTestScenario.DB);
        var configuredSolver = SolverTestScenario.Solver(
            ownedPals: [],
            maxBreedingSteps: 1,
            maxWildPals: 1,
            allowedWildPals: [katress]
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB
        ).Build(Target());

        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3 },
            seeds
                .OfType<WildPalReference>()
                .Select(reference =>
                    reference.EffectivePassives.Count(
                        passive => passive is RandomPassiveSkill
                    )
                )
                .Order()
                .ToArray()
        );
    }

    private static PalSpecifier Target() =>
        new()
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
        };
}
