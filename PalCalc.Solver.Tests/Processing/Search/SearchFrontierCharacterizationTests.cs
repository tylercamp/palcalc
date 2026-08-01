using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests.Processing.Search;

[TestClass]
public class SearchFrontierCharacterizationTests
{
    private static readonly ResultPruningPolicy MinimumEffortOnly =
        new(token => [new MinimumEffortPruning(token)]);

    [TestMethod]
    public void Results_PreserveSeparateExactEffortTiers()
    {
        var targetPal = "Katress".ToPal(SolverTestScenario.DB);
        var faster = new TestPalReference("faster", targetPal, TimeSpan.FromMinutes(5));
        var slower = new TestPalReference("slower", targetPal, TimeSpan.FromMinutes(10));
        var frontier = FrontierFor(targetPal, [faster, slower]);

        var results = frontier.TerminalResults.Results.ToList();

        CollectionAssert.AreEquivalent(new[] { faster, slower }, results);
    }

    [TestMethod]
    public void ExpandSingles_ReplacesInferiorCandidateAndReportsDelta()
    {
        var targetPal = "Anubis".ToPal(SolverTestScenario.DB);
        var statePal = "Katress".ToPal(SolverTestScenario.DB);
        var slower = new TestPalReference("slower", statePal, TimeSpan.FromMinutes(10));
        var faster = new TestPalReference("faster", statePal, TimeSpan.FromMinutes(5));
        var frontier = FrontierFor(targetPal, [slower]);

        var delta = frontier.ExpandSingles(_ => [faster]);

        Assert.IsTrue(delta.Changed);
        CollectionAssert.AreEqual(
            new[] { faster },
            delta.Added.ToArray()
        );
        CollectionAssert.AreEquivalent(
            new[] { slower },
            delta.Removed.ToArray()
        );
        CollectionAssert.AreEquivalent(new[] { faster }, frontier.CurrentContent.ToList());
    }

    [TestMethod]
    public void ExpandPairs_SchedulesOnlyPairsMadeRelevantByAddedCandidate()
    {
        var targetPal = "Anubis".ToPal(SolverTestScenario.DB);
        var first = new TestPalReference(
            "first",
            "Katress".ToPal(SolverTestScenario.DB),
            TimeSpan.FromMinutes(1)
        );
        var second = new TestPalReference(
            "second",
            "Wixen".ToPal(SolverTestScenario.DB),
            TimeSpan.FromMinutes(2)
        );
        var added = new TestPalReference(
            "added",
            "Wixen Noct".ToPal(SolverTestScenario.DB),
            TimeSpan.FromMinutes(3)
        );
        var frontier = FrontierFor(targetPal, [first, second]);
        List<(IPalReference, IPalReference)> initialPairs = [];
        List<(IPalReference, IPalReference)> deltaPairs = [];

        var initialDelta = frontier.ExpandPairs(work =>
        {
            initialPairs.AddRange(work.Chunks(100).SelectMany(c => c));
            return [added];
        });
        var unchangedDelta = frontier.ExpandPairs(work =>
        {
            deltaPairs.AddRange(work.Chunks(100).SelectMany(c => c));
            return [];
        });

        Assert.IsTrue(initialDelta.Changed);
        Assert.IsFalse(unchangedDelta.Changed);

        CollectionAssert.AreEquivalent(
            new[] { "first+first", "first+second", "second+second" },
            initialPairs.Select(NormalizedPairName).Distinct().ToArray()
        );
        CollectionAssert.AreEquivalent(
            new[] { "added+added", "added+first", "added+second" },
            deltaPairs.Select(NormalizedPairName).Distinct().ToArray()
        );
        Assert.IsTrue(
            deltaPairs.All(pair => ReferenceEquals(pair.Item1, added) || ReferenceEquals(pair.Item2, added))
        );
    }

    [TestMethod]
    public void ExpandSingles_DoesNotRetainCompletedBredTargetWithoutMissingOptionals()
    {
        var targetPal = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var parents = ParentReferences();
        var completedTarget = BredTarget(targetPal, parents.MaleKatress, parents.FemaleWixen);
        var frontier = FrontierFor(targetPal, [parents.MaleKatress, parents.FemaleWixen]);

        var delta = frontier.ExpandSingles(_ => [completedTarget]);

        Assert.IsFalse(delta.Changed);
        Assert.IsFalse(frontier.CurrentContent.Contains(completedTarget));
        CollectionAssert.Contains(
            frontier.TerminalResults.Results.ToList(),
            completedTarget
        );
    }

    [TestMethod]
    public void ExpandSingles_RetainsBredTargetWhenOptionalPassiveIsStillMissing()
    {
        var targetPal = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var optional = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var parents = ParentReferences();
        var incompleteTarget = BredTarget(targetPal, parents.MaleKatress, parents.FemaleWixen);
        var frontier = FrontierFor(
            targetPal,
            [parents.MaleKatress, parents.FemaleWixen],
            optionalPassives: [optional]
        );

        var delta = frontier.ExpandSingles(_ => [incompleteTarget]);

        Assert.IsTrue(delta.Changed);
        CollectionAssert.Contains(frontier.CurrentContent.ToList(), incompleteTarget);
        CollectionAssert.Contains(
            frontier.TerminalResults.Results.ToList(),
            incompleteTarget
        );
    }

    [TestMethod]
    public void ExpandSingles_RemovesObsoleteParentsFromPendingPairSchedule()
    {
        var targetPal = "Anubis".ToPal(SolverTestScenario.DB);
        var statePal = "Katress".ToPal(SolverTestScenario.DB);
        var other = new TestPalReference(
            "other",
            "Wixen".ToPal(SolverTestScenario.DB),
            TimeSpan.FromMinutes(2)
        );
        var slower = new TestPalReference(
            "slower",
            statePal,
            TimeSpan.FromMinutes(10)
        );
        var faster = new TestPalReference(
            "faster",
            statePal,
            TimeSpan.FromMinutes(5)
        );
        var frontier = FrontierFor(targetPal, [slower, other]);

        frontier.ExpandSingles(_ => [faster]);

        List<(IPalReference, IPalReference)> scheduledPairs = [];
        frontier.ExpandPairs(work =>
        {
            scheduledPairs.AddRange(work.Chunks(100).SelectMany(chunk => chunk));
            return [];
        });

        Assert.IsFalse(
            scheduledPairs.Any(pair =>
                ReferenceEquals(pair.Item1, slower) ||
                ReferenceEquals(pair.Item2, slower)
            )
        );
        CollectionAssert.AreEquivalent(
            new[] { "faster+faster", "faster+other", "other+other" },
            scheduledPairs
                .Select(NormalizedPairName)
                .Distinct()
                .ToArray()
        );
    }

    [TestMethod]
    public void MarkCandidatesOutdated_OnlyUpdatesMatchingEffectiveProperties()
    {
        var targetPal = "Anubis".ToPal(SolverTestScenario.DB);
        var statePal = "Katress".ToPal(SolverTestScenario.DB);
        var matching = new TestPalReference(
            "matching",
            statePal,
            TimeSpan.FromMinutes(1),
            PalGender.MALE
        );
        var otherGender = new TestPalReference(
            "other-gender",
            statePal,
            TimeSpan.FromMinutes(1),
            PalGender.FEMALE
        );
        var frontier = FrontierFor(targetPal, [matching, otherGender]);

        frontier.MarkCandidatesOutdated(
            DefaultEffectivePropertiesKeyProvider.Instance.KeyOf(matching)
        );

        Assert.IsTrue(matching.IsOutdated);
        Assert.IsFalse(otherGender.IsOutdated);
    }

    private static string NormalizedPairName((IPalReference, IPalReference) pair)
    {
        var names = new[] { pair.Item1.ToString(), pair.Item2.ToString() };
        Array.Sort(names, StringComparer.Ordinal);
        return string.Join("+", names);
    }

    private static (
        OwnedPalReference MaleKatress,
        OwnedPalReference FemaleWixen
    ) ParentReferences() =>
        (
            new OwnedPalReference(
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                effectivePassives: [],
                effectiveIVs: new IV_Set()
            ),
            new OwnedPalReference(
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
                effectivePassives: [],
                effectiveIVs: new IV_Set()
            )
        );

    private static BredPalReference BredTarget(
        Pal targetPal,
        IPalReference parent1,
        IPalReference parent2
    ) =>
        new(
            gameSettings: new GameSettings(),
            pal: targetPal,
            parent1: parent1,
            parent2: parent2,
            passives: [],
            passivesProbability: 1,
            ivs: new IV_Set(),
            ivsProbability: 1
        );

    private static SearchFrontier FrontierFor(
        Pal targetPal,
        IEnumerable<IPalReference> initialContent,
        IEnumerable<PassiveSkill>? optionalPassives = null
    )
    {
        var controller = new SolverStateController(
            CancellationToken.None
        );

        return new(
            target: new PalSpecifier
            {
                Pal = targetPal,
                OptionalPassives = optionalPassives?.ToList() ?? [],
            },
            initialContent: initialContent,
            maxThreads: 1,
            controller: controller,
            selectionPolicy: new DefaultCandidateSelectionPolicy(
                MinimumEffortOnly,
                controller.CancellationToken
            )
        );
    }

    /*
     * Deliberately minimal reference used to characterize frontier mechanics without
     * involving breeding probability or PalReference construction behavior.
     */
    private sealed class TestPalReference(
        string name,
        Pal pal,
        TimeSpan breedingEffort,
        PalGender gender = PalGender.MALE
    ) : IPalReference
    {
        public string Name { get; } = name;
        public Pal Pal { get; } = pal;
        public List<PassiveSkill> EffectivePassives { get; } = [];
        public int EffectivePassivesHash { get; } = Array.Empty<PassiveSkill>().SetHash();
        public IV_Set IVs { get; } = new();
        public List<PassiveSkill> ActualPassives { get; } = [];
        public PalGender Gender { get; } = gender;
        public float TimeFactor => 1;
        public IPalRefLocation Location => BredRefLocation.Instance;
        public TimeSpan BreedingEffort { get; } = breedingEffort;
        public TimeSpan SelfBreedingEffort { get; } = breedingEffort;
        public int TotalCost => 0;
        public int NumTotalBreedingSteps => 0;
        public int NumTotalEggs => 0;
        public int NumTotalWildPals => 0;
        public bool IsOutdated { get; set; }

        public IPalReference WithGuaranteedGender(PalDB db, PalGender requestedGender, bool useReverser) =>
            requestedGender == Gender
                ? this
                : new TestPalReference(Name, Pal, BreedingEffort, requestedGender);

        public override string ToString() => Name;
    }
}
