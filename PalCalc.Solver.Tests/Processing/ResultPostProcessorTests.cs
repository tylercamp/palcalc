using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class ResultPostProcessorTests
{
    [TestMethod]
    public void ApplySurgery_AddsMissingRequiredPassive()
    {
        var surgeryPassive =
            SolverTestScenario.DB.SurgeryPassiveSkills.First();
        var owned = SolverTestScenario.Owned(
            "Wixen Noct",
            PalGender.FEMALE
        );
        var configuredSolver = SolverTestScenario.Solver(
            [owned],
            maxSpecialCakes: 0,
            maxBreedingSteps: 0,
            maxSurgeryCost: surgeryPassive.SurgeryCost,
            allowedSurgeryPassives: [surgeryPassive]
        );
        var target = new PalSpecifier
        {
            Pal = owned.Pal,
            RequiredPassives = [surgeryPassive],
        };
        var controller = Controller();
        var policy = Policy(controller);
        var ownedReference = new OwnedPalReference(
            owned,
            effectivePassives: [],
            effectiveIVs: new IV_Set(),
            actualAttack: null,
            effectiveAttack: null,
            attackProfile: AttackProfile.Inactive,
            hasNeutralAttack: false
        );
        var frontier = new SearchFrontier(
            target,
            [ownedReference],
            maxThreads: 1,
            controller,
            policy,
            attackTargets: null
        );
        var processor = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets: null
        );

        processor.ApplySurgery(frontier);
        var results = processor.Finalize(
            frontier.TerminalResults
        );

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(
            results.All(reference =>
                reference.EffectivePassives.Contains(
                    surgeryPassive
                )
            )
        );
        Assert.IsTrue(
            results.Any(
                reference =>
                    reference is SurgeryTablePalReference
            )
        );
    }

    [TestMethod]
    public void Finalize_EnforcesRequiredGender()
    {
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredGender = PalGender.MALE,
        };
        var configuredSolver = SolverTestScenario.Solver(
            ownedPals: [],
            maxSpecialCakes: 0
        );
        var controller = Controller();
        var accumulator = new ResultAccumulator(
            target,
            Policy(controller),
            attackTargets: null
        );
        accumulator.Observe([
            new WildPalReference(
                target.Pal,
                guaranteedPassives: [],
                numRandomPassives: 0,
                mechanics: SolverTestScenario.DB.BreedingMechanics,
                actualAttack: null,
                effectiveAttack: null,
                attackProfile: AttackProfile.Inactive,
                hasNeutralAttack: false
            ),
        ]);
        var processor = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets: null
        );

        var results = processor.Finalize(accumulator);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(PalGender.MALE, results[0].Gender);
    }

    [TestMethod]
    public void Finalize_FiltersOwnedResultWithTooManyIrrelevantPassives()
    {
        var irrelevant =
            "Swift".ToStandardPassive(SolverTestScenario.DB);
        var owned = SolverTestScenario.Owned(
            "Wixen Noct",
            PalGender.FEMALE,
            passives: [irrelevant]
        );
        var target = new PalSpecifier
        {
            Pal = owned.Pal,
        };
        var configuredSolver = SolverTestScenario.Solver([owned], maxSpecialCakes: 0);
        var controller = Controller();
        var accumulator = new ResultAccumulator(
            target,
            Policy(controller),
            attackTargets: null
        );
        accumulator.Observe([
            new OwnedPalReference(
                owned,
                owned.PassiveSkills.ToDedicatedPassives(
                    target.DesiredPassives
                ),
                new IV_Set(),
                actualAttack: null,
                effectiveAttack: null,
                attackProfile: AttackProfile.Inactive,
                hasNeutralAttack: false
            ),
        ]);
        var processor = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets: null
        );

        var results = processor.Finalize(accumulator);

        Assert.AreEqual(0, results.Count);
    }

    private static SolverStateController Controller() =>
        new(CancellationToken.None);

    private static DefaultCandidateSelectionPolicy Policy(
        SolverStateController controller
    ) =>
        new(
            ResultPruningPolicy.Default,
            controller.CancellationToken,
            attackTargets: null
        );
}
