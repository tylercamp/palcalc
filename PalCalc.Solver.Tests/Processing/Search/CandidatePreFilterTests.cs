using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests.Processing.Search;

[TestClass]
public class CandidatePreFilterTests
{
    [TestMethod]
    public void TryAdd_DoesNotMaterializeAnInferiorAttackCandidate()
    {
        var child = "Katress".ToPal(SolverTestScenario.DB);
        var requiredAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !child.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredAttacks = [requiredAttack],
        };
        var configuredSolver = SolverTestScenario.Solver([], maxSpecialCakes: 0);
        var settings = configuredSolver.Settings;
        var attackTargets = new AttackTargetContext(target, settings.DB);
        var selectionPolicy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            CancellationToken.None,
            attackTargets
        );
        var incumbent = new OwnedPalReference(
            SolverTestScenario.Owned("Katress", PalGender.WILDCARD),
            [],
            new IV_Set(),
            new AttackProfile(
                new AttackProfileEntry(0, 0),
                new AttackProfileEntry(1, 0)
            )
        );
        var frontier = new SearchFrontier(
            target,
            [incumbent],
            maxThreads: 1,
            new SolverStateController(CancellationToken.None),
            selectionPolicy,
            attackTargets
        );
        var filter = new CandidatePreFilter(
            target,
            settings.MaxEffort,
            selectionPolicy,
            frontier,
            settings.DB.PalsById.Keys,
            attackTargets,
            settings
        );
        var parent1 = Candidate(cakes: 0);
        var parent2 = Candidate(cakes: 0);
        var preparedProfile = new AttackProfileComposer(attackTargets, settings).Prepare(
            child,
            parent1,
            parent2,
            passivesProbability: 1,
            ivsProbability: 1
        );
        var draft = new CandidateDraft(
            settings.GameSettings,
            child,
            parent1,
            parent2,
            [],
            passivesProbability: 1,
            new IV_Set(),
            ivsProbability: 1,
            selfBreedingEffort: TimeSpan.FromMinutes(1),
            breedingEffort: TimeSpan.FromMinutes(1),
            preparedProfile
        );

        var result = filter.TryAdd(ref draft);

        Assert.IsFalse(result.Accepted);
        Assert.IsFalse(draft.IsMaterialized);
    }

    [TestMethod]
    public void RetainedAttackCandidates_ExcludesReplacedChampions()
    {
        var requiredAttack = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit);
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredAttacks = [requiredAttack],
        };
        var configuredSolver = SolverTestScenario.Solver([], maxSpecialCakes: 10);
        var settings = configuredSolver.Settings;
        var attackTargets = new AttackTargetContext(target, settings.DB);
        var selectionPolicy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            CancellationToken.None,
            attackTargets
        );
        var filter = new CandidatePreFilter(
            target,
            settings.MaxEffort,
            selectionPolicy,
            new PotentialImprovementFrontier(),
            settings.DB.PalsById.Keys,
            attackTargets,
            settings
        );
        var first = Candidate(cakes: 2);
        var better = Candidate(cakes: 1);

        Assert.IsTrue(filter.TryAdd(first).Accepted);
        Assert.IsTrue(filter.TryAdd(better).Accepted);

        CollectionAssert.AreEqual(
            new IPalReference[] { better },
            filter.RetainedAttackCandidates()
        );
        Assert.IsTrue(first.IsOutdated);
    }

    [TestMethod]
    public void TryAdd_ReportsOwnershipForTerminalCandidateRejectedFromIteration()
    {
        var requiredAttack = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit);
        var target = new PalSpecifier
        {
            Pal = "Katress".ToPal(SolverTestScenario.DB),
            RequiredGender = PalGender.FEMALE,
            RequiredAttacks = [requiredAttack],
        };
        var configuredSolver = SolverTestScenario.Solver([], maxSpecialCakes: 0);
        var settings = configuredSolver.Settings;
        var attackTargets = new AttackTargetContext(target, settings.DB);
        var selectionPolicy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            CancellationToken.None,
            attackTargets
        );
        var filter = new CandidatePreFilter(
            target,
            settings.MaxEffort,
            selectionPolicy,
            new PotentialImprovementFrontier(),
            settings.DB.PalsById.Keys,
            attackTargets,
            settings
        );
        var first = new HashControlledReference(target.Pal, hash: 1, genderedHash: 10);
        var terminalOnly = new HashControlledReference(target.Pal, hash: 2, genderedHash: 5);

        Assert.IsTrue(filter.TryAdd(first).Accepted);

        var result = filter.TryAdd(terminalOnly);

        Assert.IsFalse(result.Accepted);
        Assert.IsTrue(result.IsRetained);
        Assert.AreEqual(5, filter.TerminalCandidates.Single().GetHashCode());
    }

    private static OwnedPalReference Candidate(int cakes) =>
        new(
            SolverTestScenario.Owned("Katress", PalGender.MALE),
            [],
            new IV_Set(),
            new AttackProfile(new AttackProfileEntry(1, cakes))
        );

    private sealed class PotentialImprovementFrontier : ICandidateFrontierView
    {
        public FrontierCandidateAssessment AssessCandidate(
            IPalReference candidate,
            EffectivePropertiesKey propertiesKey
        ) => FrontierCandidateAssessment.PotentialImprovement;

    }

    private sealed class HashControlledReference(
        Pal pal,
        int hash,
        int genderedHash,
        PalGender gender = PalGender.WILDCARD
    ) : IPalReference
    {
        public Pal Pal { get; } = pal;
        public List<PassiveSkill> EffectivePassives { get; } = [];
        public int EffectivePassivesHash => 0;
        public IV_Set IVs { get; } = new();
        public List<PassiveSkill> ActualPassives { get; } = [];
        public AttackProfile AttackProfile { get; } = new(new AttackProfileEntry(1, 0));
        public PalGender Gender { get; } = gender;
        public float TimeFactor => 1;
        public IPalRefLocation Location => BredRefLocation.Instance;
        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;
        public int TotalCost => 0;
        public int NumTotalBreedingSteps => 0;
        public int NumTotalEggs => 0;
        public int NumTotalWildPals => 0;
        public bool IsOutdated { get; set; }

        public IPalReference WithGuaranteedGender(
            PalDB db,
            PalGender requestedGender,
            bool useReverser
        ) => new HashControlledReference(Pal, genderedHash, genderedHash, requestedGender);

        public override int GetHashCode() => hash;
    }
}
