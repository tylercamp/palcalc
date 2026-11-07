using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Tests.Processing.Search;

[TestClass]
public class CandidateCostFrontierTests
{
    [TestMethod]
    [DataRow((byte)0, false)]
    [DataRow((byte)0, true)]
    [DataRow((byte)63, false)]
    [DataRow((byte)63, true)]
    public void RetainsCakeEffortTradeoffsAndRemovesDominatedPaths(byte mask, bool reverse)
    {
        var frontier = new CandidateCostFrontier(hasRequiredAttacks: true, markReplacedOutdated: true);
        var slow = Candidate(mask, cakes: 0, attempts: 100);
        var middle = Candidate(mask, cakes: 3, attempts: 10);
        var fast = Candidate(mask, cakes: 10, attempts: 1);
        var paths = new[] { slow, middle, fast };
        foreach (var path in reverse ? paths.AsEnumerable().Reverse() : paths)
            Assert.IsTrue(frontier.TryAdd(path));
        CollectionAssert.AreEquivalent(paths, frontier.Candidates.ToArray());

        var dominated = Candidate(mask, cakes: 4, attempts: 20);
        Assert.IsFalse(frontier.TryAdd(dominated));
        var improvement = Candidate(mask, cakes: 2, attempts: 1);
        Assert.IsTrue(frontier.TryAdd(improvement));
        CollectionAssert.AreEquivalent(new[] { slow, improvement }, frontier.Candidates.ToArray());
        Assert.IsTrue(middle.IsOutdated);
        Assert.IsTrue(fast.IsOutdated);
        Assert.IsFalse(slow.IsOutdated);
    }

    [TestMethod]
    public void DraftPathRetainsTradeoffsWithoutAllocatingRejectedTrees()
    {
        var settings = SolverTestScenario.Solver([]).Settings;
        var frontier = new CandidateCostFrontier(hasRequiredAttacks: false);
        var slow = Candidate(0, cakes: 0, attempts: 100);
        Assert.IsTrue(frontier.TryAdd(slow));
        var fast = Draft(cakes: 1, effort: slow.BreedingEffort / 10);
        Assert.IsTrue(frontier.TryAdd(ref fast));
        Assert.IsTrue(fast.IsMaterialized);
        var dominated = Draft(cakes: 2, effort: slow.BreedingEffort / 5);
        Assert.IsFalse(frontier.TryAdd(ref dominated));
        Assert.IsFalse(dominated.IsMaterialized);
        Assert.AreEqual(2, frontier.Candidates.Count());

        CandidateDraft Draft(int cakes, TimeSpan effort) => new(
            settings.GameSettings, slow.Pal, slow.Parent1, slow.Parent2, [],
            new(BreedingMethod.SpecialCake, 1f / cakes), new IV_Set(), 1, cakes, effort, effort,
            AttackProfile.Inactive);
    }

    [TestMethod]
    public void FullSelectionPreservesMiddleTradeoffInPassiveOnlySearch()
    {
        var target = new PalSpecifier { Pal = "Wixen Noct".ToPal(SolverTestScenario.DB) };
        var context = new AttackTargetContext(target, SolverTestScenario.DB);
        var settings = SolverTestScenario.Solver([]).Settings;
        var policy = new DefaultCandidateSelectionPolicy(settings.ResultPruning, CancellationToken.None, context, considerPassiveCakes: true);
        var paths = new[] { Candidate(0, 0, 100, passiveOnly: true), Candidate(0, 3, 10, passiveOnly: true), Candidate(0, 10, 1, passiveOnly: true) };
        CollectionAssert.AreEquivalent(paths, policy.SelectRetainedAlternatives(paths).ToArray());
    }

    [TestMethod]
    public void ReplacingOneMaskDoesNotRetireAPathStillProvidingAnotherMask()
    {
        var frontier = new CandidateCostFrontier(hasRequiredAttacks: true, markReplacedOutdated: true);
        var original = Candidate(0, cakes: 5, attempts: 10, secondMask: 63);
        Assert.IsTrue(frontier.TryAdd(original));
        Assert.IsTrue(frontier.TryAdd(Candidate(0, cakes: 0, attempts: 1)));
        Assert.IsFalse(original.IsOutdated);
        Assert.IsTrue(frontier.Candidates.Contains(original));

        Assert.IsTrue(frontier.TryAdd(Candidate(63, cakes: 0, attempts: 1)));
        Assert.IsTrue(original.IsOutdated);
        Assert.IsFalse(frontier.Candidates.Contains(original));
        Assert.AreEqual(2, frontier.Candidates.Count());
    }

    [TestMethod]
    public void EmptyAttackOutcomeUsesBitZeroAndSixAttackOutcomeUsesBit63()
    {
        var profile = new AttackProfile((byte)0, (byte)63);
        Assert.AreEqual(1UL | (1UL << 63), profile.EntryTargetMasks);
        Assert.IsTrue(profile.Contains(0));
        Assert.IsTrue(profile.Contains(63));
        Assert.IsFalse(AttackProfile.Inactive.Contains(0));
    }

    private static BredPalReference Candidate(byte mask, int cakes, int attempts, byte? secondMask = null, bool passiveOnly = false)
    {
        var settings = SolverTestScenario.Solver([]).Settings;
        var first = new OwnedPalReference(SolverTestScenario.Owned("Katress", PalGender.MALE), [],
            new IV_Set(), new AttackProfile((byte)0));
        var second = new OwnedPalReference(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), [],
            new IV_Set(), new AttackProfile((byte)0));
        return new(settings.GameSettings, "Wixen Noct".ToPal(settings.DB), PalGender.WILDCARD,
            first, second, attempts, [], new(BreedingMethod.Ordinary, 1f / attempts), new IV_Set(), 1,
            passiveOnly ? AttackProfile.Inactive : new AttackProfile(secondMask is byte other
                ? [mask, other]
                : [mask]), null, recordedSpecialCakes: cakes);
    }
}
