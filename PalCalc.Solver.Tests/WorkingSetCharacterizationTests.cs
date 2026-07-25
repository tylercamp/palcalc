using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests;

[TestClass]
public class WorkingSetCharacterizationTests
{
    private static readonly PruningRulesBuilder MinimumEffortOnly =
        new(token => [new MinimumEffortPruning(token)]);

    [TestMethod]
    public void Result_PreservesSeparateExactEffortTiers()
    {
        var targetPal = "Katress".ToPal(SolverTestScenario.DB);
        var faster = new TestPalReference("faster", targetPal, TimeSpan.FromMinutes(5));
        var slower = new TestPalReference("slower", targetPal, TimeSpan.FromMinutes(10));
        var workingSet = WorkingSetFor(targetPal, [faster, slower]);

        var results = workingSet.Result.ToList();

        CollectionAssert.AreEquivalent(new[] { faster, slower }, results);
    }

    [TestMethod]
    public void UpdateBySingle_ReplacesInferiorCandidateInSameState()
    {
        var targetPal = "Anubis".ToPal(SolverTestScenario.DB);
        var statePal = "Katress".ToPal(SolverTestScenario.DB);
        var slower = new TestPalReference("slower", statePal, TimeSpan.FromMinutes(10));
        var faster = new TestPalReference("faster", statePal, TimeSpan.FromMinutes(5));
        var workingSet = WorkingSetFor(targetPal, [slower]);

        var changed = workingSet.UpdateBySingle(_ => [faster]);

        Assert.IsTrue(changed);
        CollectionAssert.AreEquivalent(new[] { faster }, workingSet.CurrentContent.ToList());
    }

    [TestMethod]
    public void UpdateByPairs_SchedulesOnlyPairsMadeRelevantByAddedCandidate()
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
        var workingSet = WorkingSetFor(targetPal, [first, second]);
        List<(IPalReference, IPalReference)> initialPairs = [];
        List<(IPalReference, IPalReference)> deltaPairs = [];

        var initiallyChanged = workingSet.UpdateByPairs(work =>
        {
            initialPairs.AddRange(work.Chunks(100).SelectMany(c => c));
            return [added];
        });
        var changedByDelta = workingSet.UpdateByPairs(work =>
        {
            deltaPairs.AddRange(work.Chunks(100).SelectMany(c => c));
            return [];
        });

        Assert.IsTrue(initiallyChanged);
        Assert.IsFalse(changedByDelta);

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
    public void UpdateBySingle_DoesNotRetainCompletedBredTargetWithoutMissingOptionals()
    {
        var targetPal = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var parents = ParentReferences();
        var completedTarget = BredTarget(targetPal, parents.MaleKatress, parents.FemaleWixen);
        var workingSet = WorkingSetFor(targetPal, [parents.MaleKatress, parents.FemaleWixen]);

        var changed = workingSet.UpdateBySingle(_ => [completedTarget]);

        Assert.IsFalse(changed);
        Assert.IsFalse(workingSet.CurrentContent.Contains(completedTarget));
        CollectionAssert.Contains(workingSet.Result.ToList(), completedTarget);
    }

    [TestMethod]
    public void UpdateBySingle_RetainsBredTargetWhenOptionalPassiveIsStillMissing()
    {
        var targetPal = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var optional = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var parents = ParentReferences();
        var incompleteTarget = BredTarget(targetPal, parents.MaleKatress, parents.FemaleWixen);
        var workingSet = WorkingSetFor(
            targetPal,
            [parents.MaleKatress, parents.FemaleWixen],
            optionalPassives: [optional]
        );

        var changed = workingSet.UpdateBySingle(_ => [incompleteTarget]);

        Assert.IsTrue(changed);
        CollectionAssert.Contains(workingSet.CurrentContent.ToList(), incompleteTarget);
        CollectionAssert.Contains(workingSet.Result.ToList(), incompleteTarget);
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

    private static WorkingSet WorkingSetFor(
        Pal targetPal,
        IEnumerable<IPalReference> initialContent,
        IEnumerable<PassiveSkill>? optionalPassives = null
    ) =>
        new(
            target: new PalSpecifier
            {
                Pal = targetPal,
                OptionalPassives = optionalPassives?.ToList() ?? [],
            },
            pruningRulesBuilder: MinimumEffortOnly,
            initialContent: initialContent,
            maxThreads: 1,
            controller: new SolverStateController
            {
                CancellationToken = CancellationToken.None,
            }
        );

    /*
     * Deliberately minimal reference used to characterize WorkingSet mechanics without
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
