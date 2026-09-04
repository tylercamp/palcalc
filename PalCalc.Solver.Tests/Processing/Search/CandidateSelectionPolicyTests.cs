using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests.Processing.Search;

[TestClass]
public class CandidateSelectionPolicyTests
{
    [TestMethod]
    public void DefaultPolicy_UsesEarlyEffortAndCostComparison()
    {
        var policy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            CancellationToken.None,
            attackTargets: null
        );
        var incumbent = new TestPalReference(
            "incumbent",
            "Katress".ToPal(SolverTestScenario.DB),
            TimeSpan.FromMinutes(10),
            totalCost: 100
        );

        Assert.AreEqual(
            EarlyCandidateSelection.ReplaceIncumbent,
            policy.SelectEarlyCandidate(
                new TestPalReference(
                    "faster",
                    incumbent.Pal,
                    TimeSpan.FromMinutes(9),
                    totalCost: 1_000
                ),
                incumbent
            )
        );
        Assert.AreEqual(
            EarlyCandidateSelection.RejectCandidate,
            policy.SelectEarlyCandidate(
                new TestPalReference(
                    "slower",
                    incumbent.Pal,
                    TimeSpan.FromMinutes(11),
                    totalCost: 0
                ),
                incumbent
            )
        );
        Assert.AreEqual(
            EarlyCandidateSelection.ReplaceIncumbent,
            policy.SelectEarlyCandidate(
                new TestPalReference(
                    "cheaper",
                    incumbent.Pal,
                    incumbent.BreedingEffort,
                    totalCost: 99
                ),
                incumbent
            )
        );
        Assert.AreEqual(
            EarlyCandidateSelection.RejectCandidate,
            policy.SelectEarlyCandidate(
                new TestPalReference(
                    "costlier",
                    incumbent.Pal,
                    incumbent.BreedingEffort,
                    totalCost: 101
                ),
                incumbent
            )
        );
        Assert.AreEqual(
            EarlyCandidateSelection.KeepBoth,
            policy.SelectEarlyCandidate(
                new TestPalReference(
                    "exact-tie",
                    incumbent.Pal,
                    incumbent.BreedingEffort,
                    totalCost: incumbent.TotalCost
                ),
                incumbent
            )
        );
    }

    [TestMethod]
    public void DefaultPolicy_OnlyPrimaryImprovementAllowsImmediateObsolescence()
    {
        var policy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            CancellationToken.None,
            attackTargets: null
        );
        var incumbent = new TestPalReference(
            "incumbent",
            "Katress".ToPal(SolverTestScenario.DB),
            TimeSpan.FromMinutes(10),
            totalCost: 100
        );

        var faster = policy.AssessAgainstFrontier(
            new TestPalReference(
                "faster",
                incumbent.Pal,
                TimeSpan.FromMinutes(9),
                totalCost: 1_000
            ),
            incumbent
        );
        var cheaper = policy.AssessAgainstFrontier(
            new TestPalReference(
                "cheaper",
                incumbent.Pal,
                incumbent.BreedingEffort,
                totalCost: 99
            ),
            incumbent
        );

        Assert.AreEqual(
            FrontierCandidateAssessment.GuaranteedImprovement,
            faster
        );
        Assert.AreEqual(
            FrontierCandidateAssessment.PotentialImprovement,
            cheaper
        );
    }

    [TestMethod]
    public void ActiveProfiles_RetainUniqueEnvelopeContributorsBeyondResultLimit()
    {
        var policy = ActivePolicy(new(token => [new ResultLimitPruning(token, maxResults: 3)]));
        var candidates = new[]
        {
            Reference("first", "Katress", TimeSpan.FromMinutes(1), Profile(1, 2)),
            Reference("second", "Katress", TimeSpan.FromMinutes(2), Profile(4)),
            Reference("third", "Katress", TimeSpan.FromMinutes(3), Profile(8)),
            Reference("fourth", "Katress", TimeSpan.FromMinutes(4), Profile(16)),
        };

        var retained = policy.SelectRetainedAlternatives(candidates);

        CollectionAssert.AreEquivalent(candidates, retained.ToArray());
    }

    [TestMethod]
    public void ActiveProfiles_StillApplyExistingPruningToRedundantCandidates()
    {
        var policy = ActivePolicy(new(token => [new ResultLimitPruning(token, maxResults: 3)]));
        var candidates = Enumerable.Range(1, 4)
            .Select(index => Reference(
                $"candidate-{index}",
                "Katress",
                TimeSpan.FromMinutes(index),
                Profile(1)
            ))
            .ToList();

        var retained = policy.SelectRetainedAlternatives(candidates);

        Assert.AreEqual(3, retained.Count);
    }

    [TestMethod]
    public void ActiveProfiles_SupersetCapabilityCoversRedundantExactMask()
    {
        var policy = ActivePolicy(new(token => [new ResultLimitPruning(token, maxResults: 1)]));
        var subset = Reference("subset", "Katress", TimeSpan.FromMinutes(1), Profile(1));
        var superset = Reference("superset", "Katress", TimeSpan.FromMinutes(2), Profile(1, 2));

        var retained = policy.SelectRetainedAlternatives([subset, superset]);

        Assert.IsTrue(retained.Contains(superset));
    }

    [TestMethod]
    public void ActiveProfiles_RetainOverlappingIncomparableProfilesBeyondResultLimit()
    {
        var policy = ActivePolicy(new(token => [new ResultLimitPruning(token, maxResults: 1)]));
        var first = Reference("first", "Katress", TimeSpan.FromMinutes(1), Profile(1, 2));
        var second = Reference("second", "Katress", TimeSpan.FromMinutes(2), Profile(2, 4));

        var retained = policy.SelectRetainedAlternatives([first, second]);

        CollectionAssert.AreEquivalent(new[] { first, second }, retained.ToArray());
    }

    [TestMethod]
    public void ActiveProfiles_DoNotSplitCapabilitiesByNoopState()
    {
        var policy = ActivePolicy(new(token => [new ResultLimitPruning(token, maxResults: 1)]));
        var ordinary = Reference("ordinary", "Katress", TimeSpan.FromMinutes(1), Profile(1));
        var noop = Reference(
            "noop",
            "Katress",
            TimeSpan.FromMinutes(2),
            Profile(1),
            hasNoopAttack: true
        );

        var retained = policy.SelectRetainedAlternatives([ordinary, noop]);

        Assert.AreEqual(1, retained.Count);
        CollectionAssert.AreEqual(
            ordinary.AttackProfile.Entries.ToArray(),
            retained[0].AttackProfile.Entries.ToArray()
        );
    }

    [TestMethod]
    public void ActiveProfiles_RetainEachExactChampion()
    {
        var policy = ActivePolicy(new(token => [new MinimumEffortPruning(token)]));
        var baseline = Reference("baseline", "Katress", TimeSpan.Zero, Profile(0));
        var first = Reference("first", "Katress", TimeSpan.FromMinutes(1), Profile(1));
        var second = Reference("second", "Katress", TimeSpan.FromMinutes(2), Profile(2));
        var bundled = Reference("bundled", "Katress", TimeSpan.FromMinutes(3), Profile(1, 2));

        var retained = policy.SelectRetainedAlternatives([baseline, first, second, bundled]);

        CollectionAssert.AreEquivalent(new[] { baseline, first, second }, retained.ToArray());
    }

    [TestMethod]
    public void ActiveProfiles_DoNotDeduplicateDistinctBredCapabilities()
    {
        var policy = ActivePolicy();
        var parent1 = Reference("parent-1", "Katress", TimeSpan.Zero);
        var parent2 = Reference("parent-2", "Wixen", TimeSpan.Zero);
        var settings = new GameSettings();

        BredPalReference BredWith(AttackProfile profile) => new(
            settings,
            "Wixen Noct".ToPal(SolverTestScenario.DB),
            parent1,
            parent2,
            passives: [],
            passivesProbability: 1,
            ivs: new IV_Set(),
            ivsProbability: 1,
            attackProfile: profile,
            materializedAttackInheritance: null,
            avgRequiredBreedings: null,
            gender: PalGender.WILDCARD
        );

        var first = BredWith(Profile(1));
        var second = BredWith(Profile(2));

        var retained = policy.SelectRetainedAlternatives([first, second]);

        CollectionAssert.AreEquivalent(new[] { first, second }, retained.ToArray());
    }

    [TestMethod]
    public void ActiveProfiles_PreserveUniqueEarlyCandidates()
    {
        var policy = ActivePolicy();
        var incumbent = Reference("incumbent", "Katress", TimeSpan.FromMinutes(5), Profile(1));
        var slowerUnique = Reference("unique", "Katress", TimeSpan.FromMinutes(10), Profile(2));
        var noop = Reference(
            "neutral",
            "Katress",
            TimeSpan.FromMinutes(5),
            Profile(1),
            totalCost: 100,
            hasNoopAttack: true
        );
        var cheaperNormal = Reference(
            "non-neutral",
            "Katress",
            TimeSpan.FromMinutes(5),
            Profile(1),
            totalCost: 1
        );

        Assert.AreEqual(
            EarlyCandidateSelection.KeepBoth,
            policy.SelectEarlyCandidate(slowerUnique, incumbent)
        );
        Assert.AreEqual(
            EarlyCandidateSelection.ReplaceIncumbent,
            policy.SelectEarlyCandidate(cheaperNormal, noop)
        );
    }

    [TestMethod]
    public void ActiveProfiles_RequireCompleteCoverageForFrontierImprovement()
    {
        var policy = ActivePolicy();
        var incumbent = Reference(
            "incumbent",
            "Katress",
            TimeSpan.FromMinutes(10),
            Profile(1, 2)
        );
        var fasterPartial = Reference(
            "partial",
            "Katress",
            TimeSpan.FromMinutes(5),
            Profile(1)
        );

        Assert.AreEqual(
            FrontierCandidateAssessment.PotentialImprovement,
            policy.AssessAgainstFrontier(fasterPartial, incumbent)
        );
    }

    [TestMethod]
    public void ActiveProfiles_LowerCakesCoverHigherCakesForTheSameExactMask()
    {
        var policy = ActivePolicy();
        var lowerCakes = Reference(
            "lower-cakes",
            "Katress",
            TimeSpan.FromMinutes(5),
            ProfileWithCakes(1, cakes: 1)
        );
        var higherCakes = Reference(
            "higher-cakes",
            "Katress",
            TimeSpan.FromMinutes(10),
            ProfileWithCakes(1, cakes: 2)
        );

        Assert.AreEqual(
            EarlyCandidateSelection.ReplaceIncumbent,
            policy.SelectEarlyCandidate(lowerCakes, higherCakes)
        );
    }

    [TestMethod]
    public void ActiveProfiles_EqualCakesLetStructuralEffortDecide()
    {
        var policy = ActivePolicy();
        var faster = Reference(
            "faster",
            "Katress",
            TimeSpan.FromMinutes(5),
            ProfileWithCakes(1, cakes: 2)
        );
        var slower = Reference(
            "slower",
            "Katress",
            TimeSpan.FromMinutes(10),
            ProfileWithCakes(1, cakes: 2)
        );

        Assert.AreEqual(
            EarlyCandidateSelection.ReplaceIncumbent,
            policy.SelectEarlyCandidate(faster, slower)
        );
        Assert.AreEqual(
            EarlyCandidateSelection.RejectCandidate,
            policy.SelectEarlyCandidate(slower, faster)
        );
    }

    [TestMethod]
    public void ActiveProfiles_DifferentExactMasksDoNotCoverOneAnother()
    {
        var policy = ActivePolicy();
        var faster = Reference(
            "faster",
            "Katress",
            TimeSpan.FromMinutes(5),
            ProfileWithCakes(1, cakes: 0)
        );
        var slower = Reference(
            "slower",
            "Katress",
            TimeSpan.FromMinutes(10),
            ProfileWithCakes(2, cakes: 0)
        );

        Assert.AreEqual(
            EarlyCandidateSelection.KeepBoth,
            policy.SelectEarlyCandidate(faster, slower)
        );
        Assert.AreEqual(
            EarlyCandidateSelection.KeepBoth,
            policy.SelectEarlyCandidate(slower, faster)
        );
    }

    [TestMethod]
    public void ActiveProfiles_AllowExistingEffortPruningForIdenticalCapability()
    {
        var policy = ActivePolicy(new(token => [new MinimumEffortPruning(token)]));
        var faster = Reference("faster", "Katress", TimeSpan.FromMinutes(5), Profile(1));
        var slower = Reference("slower", "Katress", TimeSpan.FromMinutes(10), Profile(1));

        var retained = policy.SelectRetainedAlternatives([slower, faster]);

        CollectionAssert.AreEqual(new[] { faster }, retained.ToArray());
    }

    [TestMethod]
    public void ExpansionOrdering_PreservesDiscoveryOrderForEqualPriorities()
    {
        var policy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            CancellationToken.None,
            attackTargets: null
        );
        var candidates = Enumerable
            .Range(0, 32)
            .Select(index =>
                Reference(
                    $"candidate-{index}",
                    "Katress",
                    TimeSpan.FromMinutes(1)
                )
            )
            .ToList();

        var ordered = policy.OrderForExpansion(candidates);

        CollectionAssert.AreEqual(candidates, ordered);
    }

    [TestMethod]
    public void ReverseExpansionPolicy_ChangesPairOrderWithoutChangingSolverResults()
    {
        var defaultOrder = DeltaPairOrder(
            policyFactory: policy => policy
        );
        var reverseOrder = DeltaPairOrder(
            policyFactory: defaultPolicy =>
                new DelegatingPolicy(
                    defaultPolicy,
                    expansionPriorityComparer: ReverseEffortComparer.Instance
                )
        );

        CollectionAssert.AreEqual(
            new[] { "added+first", "added+second", "added+added" },
            defaultOrder
        );
        CollectionAssert.AreEqual(
            new[] { "added+second", "added+first", "added+added" },
            reverseOrder
        );

        var configuredSolver = SolverWithMultipleCandidateAlternatives();
        var baseline = SolverTestScenario.Solve(
            configuredSolver,
            "Wixen Noct",
            requiredPassives:
            [
                "Swift".ToStandardPassive(SolverTestScenario.DB),
            ],
            optionalPassives:
            [
                "Runner".ToStandardPassive(SolverTestScenario.DB),
            ],
            ivAttack: 90
        );
        var reversePolicyResults = SolveWithPolicy(
            configuredSolver,
            new DelegatingPolicy(
                DefaultPolicy(configuredSolver.Settings),
                expansionPriorityComparer: ReverseEffortComparer.Instance
            )
        );

        CollectionAssert.AreEqual(
            SolverTestScenario.Signatures(baseline).ToArray(),
            SolverTestScenario.Signatures(reversePolicyResults).ToArray()
        );
    }

    [TestMethod]
    public void PolicyCanDeclineEarlyDominanceAndStillReachExpectedFrontier()
    {
        var configuredSolver = SolverWithMultipleCandidateAlternatives();
        var earlyComparisons = 0;
        var baseline = SolverTestScenario.Solve(
            configuredSolver,
            "Wixen Noct",
            requiredPassives:
            [
                "Swift".ToStandardPassive(SolverTestScenario.DB),
            ],
            optionalPassives:
            [
                "Runner".ToStandardPassive(SolverTestScenario.DB),
            ],
            ivAttack: 90
        );
        var noEarlyDominanceResults = SolveWithPolicy(
            configuredSolver,
            new DelegatingPolicy(
                DefaultPolicy(configuredSolver.Settings),
                earlySelection: (_, _) =>
                {
                    Interlocked.Increment(ref earlyComparisons);
                    return EarlyCandidateSelection.KeepBoth;
                }
            )
        );

        Assert.IsTrue(earlyComparisons > 0);
        CollectionAssert.AreEqual(
            SolverTestScenario.Signatures(baseline).ToArray(),
            SolverTestScenario.Signatures(noEarlyDominanceResults).ToArray()
        );
    }

    private static SolverTestScenario.ConfiguredSolver SolverWithMultipleCandidateAlternatives()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var runner = "Runner".ToStandardPassive(SolverTestScenario.DB);

        return SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned(
                    "Katress",
                    PalGender.MALE,
                    passives: [swift],
                    ivAttack: 100
                ),
                SolverTestScenario.Owned(
                    "Wixen",
                    PalGender.FEMALE,
                    passives: [runner]
                ),
            ],
            maxSpecialCakes: 0,
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxThreads: 2
        );
    }

    private static List<IPalReference> SolveWithPolicy(
        SolverTestScenario.ConfiguredSolver configuredSolver,
        ICandidateSelectionPolicy policy
    )
    {
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredPassives =
            [
                "Swift".ToStandardPassive(SolverTestScenario.DB),
            ],
            OptionalPassives =
            [
                "Runner".ToStandardPassive(SolverTestScenario.DB),
            ],
            IV_Attack = 90,
        };
        var controller = new SolverStateController(
            CancellationToken.None
        );
        var context = SolverRunContext.Create(
            new BreedingSolverRequest(target, configuredSolver.Settings),
            controller,
            policy
        );

        return new SolverRun(
            context,
            _ => { },
            TimeSpan.FromSeconds(1)
        ).Execute();
    }

    private static string[] DeltaPairOrder(
        Func<
            DefaultCandidateSelectionPolicy,
            ICandidateSelectionPolicy
        > policyFactory
    )
    {
        var controller = new SolverStateController(
            CancellationToken.None
        );
        var policy = policyFactory(
            new DefaultCandidateSelectionPolicy(
                ResultPruningPolicy.Default,
                controller.CancellationToken,
                attackTargets: null
            )
        );
        var first = Reference(
            "first",
            "Katress",
            TimeSpan.FromMinutes(1)
        );
        var second = Reference(
            "second",
            "Wixen",
            TimeSpan.FromMinutes(2)
        );
        var added = Reference(
            "added",
            "Wixen Noct",
            TimeSpan.FromMinutes(3)
        );
        var frontier = new SearchFrontier(
            target: new PalSpecifier
            {
                Pal = "Anubis".ToPal(SolverTestScenario.DB),
            },
            initialContent: [first, second],
            maxThreads: 1,
            controller: controller,
            selectionPolicy: policy,
            attackTargets: null
        );

        frontier.ExpandPairs(_ => [added]);

        var orderedPairs = new List<(IPalReference, IPalReference)>();
        frontier.ExpandPairs(work =>
        {
            orderedPairs.AddRange(
                work.Chunks(100).SelectMany(chunk => chunk)
            );
            return [];
        });

        return orderedPairs
            .Select(pair =>
                string.Join(
                    "+",
                    new[]
                    {
                        ((TestPalReference)pair.Item1).Name,
                        ((TestPalReference)pair.Item2).Name,
                    }.Order(StringComparer.Ordinal)
                )
            )
            .ToArray();
    }

    private static DefaultCandidateSelectionPolicy DefaultPolicy(
        BreedingSolverSettings settings
    ) =>
        new(
            settings.ResultPruning,
            CancellationToken.None,
            attackTargets: null
        );

    private static DefaultCandidateSelectionPolicy ActivePolicy(
        ResultPruningPolicy? pruning = null
    ) =>
        new(
            pruning ?? ResultPruningPolicy.Default,
            CancellationToken.None,
            attackTargets: new AttackTargetContext(
                new PalSpecifier
                {
                    RequiredAttacks =
                    [.. SolverTestScenario.DB.ActiveSkills.Where(attack => attack.CanInherit).Take(6)],
                },
                SolverTestScenario.DB
            )
        );

    private static AttackProfile Profile(params byte[] masks) => new(masks.Select(mask =>
        new AttackProfileEntry(mask, 0)
    ).ToArray());

    private static AttackProfile ProfileWithCakes(byte mask, int cakes) =>
        new(new AttackProfileEntry(mask, cakes));

    private static TestPalReference Reference(
        string name,
        string pal,
        TimeSpan effort,
        AttackProfile attackProfile = default,
        int totalCost = 0,
        bool hasNoopAttack = false
    ) =>
        new(
            name,
            pal.ToPal(SolverTestScenario.DB),
            effort,
            totalCost,
            hasNoopAttack
                ? new AttackProfile(true, attackProfile.Entries.ToArray())
                : attackProfile
        );

    private sealed class DelegatingPolicy(
        ICandidateSelectionPolicy inner,
        IComparer<IPalReference>? expansionPriorityComparer = null,
        Func<
            IPalReference,
            IPalReference,
            EarlyCandidateSelection
        >? earlySelection = null
    ) : ICandidateSelectionPolicy
    {
        public IComparer<IPalReference> ExpansionPriorityComparer { get; } =
            expansionPriorityComparer ??
            inner.ExpansionPriorityComparer;

        public EffectivePropertiesKey KeyOf(IPalReference reference) =>
            inner.KeyOf(reference);

        public EarlyCandidateSelection SelectEarlyCandidate(
            IPalReference candidate,
            IPalReference incumbent
        ) =>
            earlySelection?.Invoke(candidate, incumbent) ??
            inner.SelectEarlyCandidate(candidate, incumbent);

        public FrontierCandidateAssessment AssessAgainstFrontier(
            IPalReference candidate,
            IPalReference incumbent
        ) =>
            inner.AssessAgainstFrontier(candidate, incumbent);

        public IReadOnlyList<IPalReference> SelectRetainedAlternatives(
            IEnumerable<IPalReference> candidates
        ) =>
            inner.SelectRetainedAlternatives(candidates);

        public BreedingEffortGroupKey BreedingEffortGroupOf(
            IPalReference candidate
        ) =>
            inner.BreedingEffortGroupOf(candidate);
    }

    private sealed class ReverseEffortComparer : IComparer<IPalReference>
    {
        public static ReverseEffortComparer Instance { get; } = new();

        public int Compare(IPalReference? left, IPalReference? right) =>
            right!.BreedingEffort.CompareTo(left!.BreedingEffort);
    }

    private sealed class RecordingPruning(
        CancellationToken token,
        string name,
        List<string> calls
    ) : ResultPruningRule(token)
    {
        public override IEnumerable<IPalReference> Apply(
            IEnumerable<IPalReference> results,
            CachedResultData cachedData
        )
        {
            calls.Add(name);
            return results;
        }
    }

    private sealed class TestPalReference(
        string name,
        Pal pal,
        TimeSpan breedingEffort,
        int totalCost = 0,
        AttackProfile attackProfile = default
    ) : IPalReference
    {
        public string Name { get; } = name;
        public Pal Pal { get; } = pal;
        public List<PassiveSkill> EffectivePassives { get; } = [];
        public int EffectivePassivesHash => 0;
        public IV_Set IVs { get; } = new();
        public List<PassiveSkill> ActualPassives { get; } = [];
        public AttackProfile AttackProfile { get; } = attackProfile;
        public PalGender Gender => PalGender.MALE;
        public float TimeFactor => 1;
        public IPalRefLocation Location => BredRefLocation.Instance;
        public TimeSpan BreedingEffort { get; } = breedingEffort;
        public TimeSpan SelfBreedingEffort { get; } = breedingEffort;
        public int TotalCost { get; } = totalCost;
        public int NumTotalBreedingSteps => 0;
        public int NumTotalEggs => 0;
        public int NumTotalWildPals => 0;
        public bool IsOutdated { get; set; }

        public IPalReference WithGuaranteedGender(
            PalDB db,
            PalGender gender,
            bool useReverser
        ) =>
            this;
    }
}
