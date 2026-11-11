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
    [DataRow((byte)0)]
    [DataRow((byte)63)]
    public void RetainsOnlyCheapestAndFastestRegardlessOfArrivalOrder(byte mask)
    {
        int[][] orders = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
        foreach (var order in orders)
        {
            var frontier = new CandidateCostFrontier(hasRequiredAttacks: true, markReplacedOutdated: true);
            var slow = Candidate(mask, cakes: 0, attempts: 100);
            var middle = Candidate(mask, cakes: 3, attempts: 10);
            var fast = Candidate(mask, cakes: 10, attempts: 1);
            var paths = new[] { slow, middle, fast };
            var middleAccepted = false;
            foreach (var index in order)
            {
                var accepted = frontier.TryAdd(paths[index]);
                if (index == 1) middleAccepted = accepted;
                Assert.IsTrue(frontier.Candidates.Count() <= 2);
            }
            CollectionAssert.AreEquivalent(new[] { slow, fast }, frontier.Candidates.ToArray());
            Assert.AreEqual(middleAccepted, middle.IsOutdated);

            var improvement = Candidate(mask, cakes: 2, attempts: 1);
            Assert.IsTrue(frontier.TryAdd(improvement));
            CollectionAssert.AreEquivalent(new[] { slow, improvement }, frontier.Candidates.ToArray());
            Assert.IsFalse(frontier.TryAdd(improvement));
            Assert.IsTrue(fast.IsOutdated);
            Assert.IsFalse(slow.IsOutdated);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DraftPathRejectsMiddleTradeoffWithoutAllocatingTree(bool hasRequiredAttacks)
    {
        var settings = SolverTestScenario.Solver([]).Settings;
        var frontier = new CandidateCostFrontier(hasRequiredAttacks, markReplacedOutdated: false);
        var slow = Candidate(63, cakes: 0, attempts: 100, passiveOnly: !hasRequiredAttacks);
        Assert.IsTrue(frontier.TryAdd(slow));
        var fast = Draft(attempts: 1, parentCakes: 9);
        Assert.IsTrue(frontier.TryAdd(ref fast));
        Assert.IsTrue(fast.IsMaterialized);
        var middle = Draft(attempts: 3, parentCakes: 0);
        Assert.IsFalse(frontier.TryAdd(ref middle));
        Assert.IsFalse(middle.IsMaterialized);
        Assert.AreEqual(2, frontier.Candidates.Count());

        CandidateDraft Draft(int attempts, int parentCakes)
        {
            var parent = new SolverTestScenario.PricedOwnedReference(
                SolverTestScenario.Owned("Katress", PalGender.MALE), [], new IV_Set(), slow.AttackProfile, parentCakes);
            var effort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
                settings.GameSettings, slow.Pal, parent.TimeFactor, slow.Parent2.TimeFactor, attempts);
            return new(settings.GameSettings, slow.Pal, parent, slow.Parent2, [],
                new(BreedingMethod.SpecialCake, 1f / attempts), new IV_Set(), 1, attempts, effort, effort, slow.AttackProfile);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FullSelectionRetainsOnlyCheapestAndFastest(bool hasRequiredAttacks)
    {
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredAttacks = hasRequiredAttacks ? SolverTestScenario.DB.ActiveSkills.Take(1).ToList() : [],
        };
        var context = new AttackTargetContext(target, SolverTestScenario.DB);
        var settings = SolverTestScenario.Solver([]).Settings;
        var policy = new DefaultCandidateSelectionPolicy(settings.ResultPruning, CancellationToken.None, context, considerPassiveCakes: !hasRequiredAttacks);
        var paths = Enumerable.Range(0, 100)
            .Select(cakes => Candidate(0, cakes, 100 - cakes, passiveOnly: !hasRequiredAttacks)).ToArray();
        CollectionAssert.AreEquivalent(new[] { paths[0], paths[^1] }, policy.SelectRetainedAlternatives(paths).ToArray());
        Assert.IsTrue(paths.All(path => !path.IsOutdated));
    }

    [TestMethod]
    public void ReplacingCheapestDoesNotRetirePathStillProvidingFastest()
    {
        var frontier = new CandidateCostFrontier(hasRequiredAttacks: true, markReplacedOutdated: true);
        var original = Candidate(1, cakes: 5, attempts: 2);
        var cheapest = Candidate(1, cakes: 0, attempts: 10);
        var fastest = Candidate(1, cakes: 6, attempts: 1);
        Assert.IsTrue(frontier.TryAdd(original));
        Assert.IsTrue(frontier.TryAdd(cheapest));
        Assert.IsFalse(original.IsOutdated);
        CollectionAssert.AreEquivalent(new[] { cheapest, original }, frontier.Candidates.ToArray());

        Assert.IsTrue(frontier.TryAdd(fastest));
        Assert.IsTrue(original.IsOutdated);
        CollectionAssert.AreEquivalent(new[] { cheapest, fastest }, frontier.Candidates.ToArray());
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
