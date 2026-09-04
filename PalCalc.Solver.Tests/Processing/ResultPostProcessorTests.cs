using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class ResultPostProcessorTests
{
    private static readonly Pal TargetPal = "Wixen Noct".ToPal(SolverTestScenario.DB);
    private static readonly ActiveSkill TargetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
        attack.CanInherit && !TargetPal.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
    );

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
            attackProfile: AttackProfile.Inactive
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
                attackProfile: AttackProfile.Inactive
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
                attackProfile: AttackProfile.Inactive
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

    [TestMethod]
    public void Finalize_ShortlistsEstimatedCakeLimitBeforeMaterialization()
    {
        var target = ActiveTarget(TargetPal, TargetAttack);
        var configuredSolver = SolverTestScenario.Solver(
            [],
            maxSpecialCakes: 0
        );
        var controller = Controller();
        var attackTargets = new AttackTargetContext(target, SolverTestScenario.DB);
        var accumulator = new ResultAccumulator(
            target,
            Policy(controller, attackTargets),
            attackTargets
        );
        var advertisedButUnreconstructable = Bred(
            Leaf(),
            Leaf(),
            TargetPal,
            new AttackProfile(new AttackProfileEntry(1, 1))
        );
        accumulator.Observe([advertisedButUnreconstructable]);

        var results = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets
        ).Finalize(accumulator);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Finalize_MaterializesOnlyTheMinimumEstimatedCakeTier()
    {
        var target = ActiveTarget(TargetPal, TargetAttack);
        var configuredSolver = SolverTestScenario.Solver(
            [],
            maxSpecialCakes: null
        );
        var controller = Controller();
        var attackTargets = new AttackTargetContext(target, SolverTestScenario.DB);
        var accumulator = new ResultAccumulator(
            target,
            new KeepAllSelectionPolicy(),
            attackTargets
        );
        var validMinimum = Bred(
            Leaf(TargetAttack, mask: 1),
            Leaf(TargetAttack, mask: 1),
            TargetPal,
            new AttackProfile(new AttackProfileEntry(1, 0))
        );
        var discardedHigherTier = Bred(
            Leaf(),
            Leaf(),
            TargetPal,
            new AttackProfile(new AttackProfileEntry(1, 1))
        );
        accumulator.Observe([validMinimum, discardedHigherTier]);

        var results = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets
        ).Finalize(accumulator);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(0, results[0].AttackProfile.Entries.Single().TotalSpecialCakes);
    }

    [TestMethod]
    public void SelectFinalResults_KeepsTheMinimumMaterializedCakeTier()
    {
        var target = ActiveTarget(TargetPal, TargetAttack);
        var controller = Controller();
        var attackTargets = new AttackTargetContext(target, SolverTestScenario.DB);
        var accumulator = new ResultAccumulator(
            target,
            new KeepAllSelectionPolicy(),
            attackTargets
        );

        var results = accumulator.SelectFinalResults([
            Leaf(TargetAttack, mask: 1, totalSpecialCakes: 2),
            Leaf(TargetAttack, mask: 1, totalSpecialCakes: 1),
        ]).ToList();

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(1, results[0].AttackProfile.Entries.Single().TotalSpecialCakes);
    }

    [TestMethod]
    public void Finalize_ChecksExactEffortAfterMaterialization()
    {
        var target = ActiveTarget(TargetPal, TargetAttack);
        var configuredSolver = SolverTestScenario.Solver(
            [],
            maxSpecialCakes: 0,
            maxEffort: TimeSpan.Zero
        );
        var controller = Controller();
        var attackTargets = new AttackTargetContext(target, SolverTestScenario.DB);
        var accumulator = new ResultAccumulator(
            target,
            Policy(controller, attackTargets),
            attackTargets
        );
        var advertised = Bred(
            Leaf(mask: 1),
            Leaf(mask: 1),
            TargetPal,
            new AttackProfile(new AttackProfileEntry(1, 0))
        );
        accumulator.Observe([advertised]);

        var results = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets
        ).Finalize(accumulator);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Finalize_ChecksGenderAdjustedCakeUseAfterMaterialization()
    {
        var child = SolverTestScenario.DB.Pals.First(p =>
            SolverTestScenario.DB.BreedingGenderProbability[p][PalGender.MALE] < 0.5f &&
            SolverTestScenario.DB.ActiveSkills.Any(attack =>
                attack.CanInherit && !p.Level1AttackInternalIds.Contains(attack.InternalName)
            )
        );
        var targetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !child.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var target = ActiveTarget(child, targetAttack, PalGender.MALE);
        var configuredSolver = SolverTestScenario.Solver(
            [],
            maxSpecialCakes: 1
        );
        var controller = Controller();
        var attackTargets = new AttackTargetContext(target, SolverTestScenario.DB);
        var accumulator = new ResultAccumulator(
            target,
            Policy(controller, attackTargets),
            attackTargets
        );
        var advertised = Bred(
            Leaf(targetAttack, mask: 1),
            Leaf(targetAttack),
            child,
            new AttackProfile(new AttackProfileEntry(1, 1))
        );
        accumulator.Observe([advertised]);

        var results = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets
        ).Finalize(accumulator);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Finalize_ContinuesAfterRejectingOneMaterializedFinalist()
    {
        var child = SolverTestScenario.DB.Pals.First(p =>
            SolverTestScenario.DB.BreedingGenderProbability[p][PalGender.MALE] < 0.5f &&
            SolverTestScenario.DB.ActiveSkills.Any(attack =>
                attack.CanInherit && !p.Level1AttackInternalIds.Contains(attack.InternalName)
            )
        );
        var targetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !child.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var target = ActiveTarget(child, targetAttack);
        var configuredSolver = SolverTestScenario.Solver(
            [],
            maxSpecialCakes: 1
        );
        var controller = Controller();
        var attackTargets = new AttackTargetContext(target, SolverTestScenario.DB);
        var accumulator = new ResultAccumulator(
            target,
            new KeepAllSelectionPolicy(),
            attackTargets
        );
        var rejected = Bred(
            Leaf(targetAttack, mask: 1),
            Leaf(targetAttack),
            child,
            new AttackProfile(new AttackProfileEntry(1, 1)),
            gender: PalGender.MALE
        );
        var valid = Bred(
            Leaf(targetAttack, mask: 1),
            Leaf(targetAttack, mask: 1),
            child,
            new AttackProfile(new AttackProfileEntry(1, 0))
        );
        accumulator.Observe([rejected, valid]);

        var results = new ResultPostProcessor(
            target,
            configuredSolver.Settings,
            controller,
            attackTargets
        ).Finalize(accumulator);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(0, results[0].AttackProfile.Entries.Single().TotalSpecialCakes);
    }

    private static PalSpecifier ActiveTarget(
        Pal child,
        ActiveSkill attack,
        PalGender requiredGender = PalGender.WILDCARD
    ) => new()
    {
        Pal = child,
        RequiredAttacks = [attack],
        RequiredGender = requiredGender,
    };

    private static BredPalReference Bred(
        IPalReference parent1,
        IPalReference parent2,
        Pal child,
        AttackProfile attackProfile,
        PalGender gender = PalGender.WILDCARD
    ) => new(
        new GameSettings(),
        child,
        parent1,
        parent2,
        [],
        passivesProbability: 1,
        new IV_Set(),
        ivsProbability: 1,
        attackProfile,
        materializedAttackInheritance: null,
        avgRequiredBreedings: null,
        gender
    );

    private static OwnedPalReference Leaf(
        ActiveSkill? attack = null,
        byte mask = 0,
        int totalSpecialCakes = 0
    )
    {
        var instance = SolverTestScenario.Owned("Katress", PalGender.MALE);
        var learnedAttack = mask != 0
            ? attack ?? TargetAttack
            : SolverTestScenario.DB.ActiveSkills.First(skill => skill != TargetAttack);
        instance.ActiveSkills = [learnedAttack];
        instance.EquippedActiveSkills = [learnedAttack];

        return new(
            instance,
            [],
            new IV_Set(),
            new AttackProfile(new AttackProfileEntry(mask, totalSpecialCakes))
        );
    }

    private static SolverStateController Controller() =>
        new(CancellationToken.None);

    private static DefaultCandidateSelectionPolicy Policy(
        SolverStateController controller,
        AttackTargetContext? attackTargets = null
    ) =>
        new(
            ResultPruningPolicy.Default,
            controller.CancellationToken,
            attackTargets
        );

    private sealed class KeepAllSelectionPolicy : ICandidateSelectionPolicy
    {
        public IComparer<IPalReference> ExpansionPriorityComparer { get; } =
            Comparer<IPalReference>.Create((left, right) =>
                left.BreedingEffort.CompareTo(right.BreedingEffort)
            );

        public EffectivePropertiesKey KeyOf(IPalReference reference) => default;

        public EarlyCandidateSelection SelectEarlyCandidate(
            IPalReference candidate,
            IPalReference incumbent
        ) => EarlyCandidateSelection.KeepBoth;

        public FrontierCandidateAssessment AssessAgainstFrontier(
            IPalReference candidate,
            IPalReference incumbent
        ) => FrontierCandidateAssessment.PotentialImprovement;

        public IReadOnlyList<IPalReference> SelectRetainedAlternatives(
            IEnumerable<IPalReference> candidates
        ) => candidates.ToList();

        public BreedingEffortGroupKey BreedingEffortGroupOf(IPalReference candidate) =>
            new(candidate.BreedingEffort.Ticks);
    }
}
