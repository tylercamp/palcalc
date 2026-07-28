using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class CandidateExpanderTests
{
    [DataTestMethod]
    [DataRow(PalGender.MALE, PalGender.FEMALE, "Wixen Noct")]
    [DataRow(PalGender.FEMALE, PalGender.MALE, "Katress Ignis")]
    public void ExpandBatch_UsesGenderSpecificBreedingResult(
        PalGender katressGender,
        PalGender wixenGender,
        string expectedChild
    )
    {
        var expansion = Expand(
            SolverTestScenario.Owned("Katress", katressGender),
            SolverTestScenario.Owned("Wixen", wixenGender),
            new PalSpecifier
            {
                Pal = expectedChild.ToPal(SolverTestScenario.DB),
            }
        );

        Assert.AreEqual(1L, expansion.Progress.NumProcessed);
        Assert.AreEqual(1, expansion.Candidates.Count);
        Assert.IsTrue(
            expansion.Candidates.All(
                candidate => candidate.Pal.Name == expectedChild
            )
        );
    }

    [TestMethod]
    public void ExpandBatch_ProducesRequiredPassiveAndIVState()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredPassives = [swift],
            IV_Attack = 90,
        };
        var expansion = Expand(
            SolverTestScenario.Owned(
                "Katress",
                PalGender.MALE,
                passives: [swift],
                ivAttack: 100
            ),
            SolverTestScenario.Owned(
                "Wixen",
                PalGender.FEMALE
            ),
            target
        );

        Assert.AreEqual(1, expansion.Candidates.Count);
        Assert.IsTrue(
            expansion.Candidates.All(
                candidate =>
                    candidate.EffectivePassives.Contains(swift) &&
                    candidate.IVs.Attack.IsRelevant &&
                    candidate.IVs.Attack.Satisfies(90)
            )
        );
    }

    [TestMethod]
    public void ExpandBatch_TreatsKnownZeroIVAsRealValue()
    {
        var expansion = Expand(
            SolverTestScenario.Owned("Katress", PalGender.MALE, ivHp: 0),
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE, ivHp: 98),
            new PalSpecifier
            {
                Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            }
        );

        var childHP = expansion.Candidates.Single().IVs.HP;
        Assert.AreEqual(0, childHP.Min);
        Assert.AreEqual(98, childHP.Max);
    }

    private static ExpansionResult Expand(
        PalInstance first,
        PalInstance second,
        PalSpecifier target
    )
    {
        var configuredSolver = SolverTestScenario.Solver(
            [first, second],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );
        var settings = configuredSolver.Settings;
        var controller = new SolverStateController(
            CancellationToken.None
        );
        var selectionPolicy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            controller.CancellationToken
        );
        var firstReference = ReferenceFor(first, target);
        var secondReference = ReferenceFor(second, target);
        var frontier = new SearchFrontier(
            target,
            [firstReference, secondReference],
            maxThreads: 1,
            controller,
            selectionPolicy
        );
        var context = new CandidateExpansionContext(
            StepIndex: 0,
            Target: target,
            PreFilter: new CandidatePreFilter(
                target,
                settings.MaxEffort,
                selectionPolicy,
                frontier,
                settings.DB.PalsById.Keys
            )
        );
        var progress = new WorkBatchProgress();
        var expander = new CandidateExpander(
            controller,
            settings,
            new ObjectPoolFactory(),
            settings.DB.BreedingMechanics,
            settings.BreedingDB
        );

        var candidates = expander
            .ExpandBatch(
                [(firstReference, secondReference)],
                progress,
                context
            )
            .ToList();

        return new(candidates, progress);
    }

    private static OwnedPalReference ReferenceFor(
        PalInstance instance,
        PalSpecifier target
    ) =>
        new(
            instance,
            instance.PassiveSkills.ToDedicatedPassives(
                target.DesiredPassives
            ),
            new IV_Set(
                HP: EffectiveIV(target.IV_HP, instance.IV_HP),
                Attack: EffectiveIV(
                    target.IV_Attack,
                    instance.IV_Attack
                ),
                Defense: EffectiveIV(
                    target.IV_Defense,
                    instance.IV_Defense
                )
            )
        );

    private static IV_Value EffectiveIV(int minimum, int value) =>
        new(
            IsRelevant: minimum != 0 && value >= minimum,
            Min: value,
            Max: value
        );

    private sealed record ExpansionResult(
        List<IPalReference> Candidates,
        WorkBatchProgress Progress
    );
}
