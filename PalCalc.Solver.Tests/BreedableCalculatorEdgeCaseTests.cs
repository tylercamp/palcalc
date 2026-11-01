using PalCalc.Model;

namespace PalCalc.Solver.Tests;

// These use made-up breeding data to build shapes which the real data doesn't contain, or tests invariants against
// random selections of real pals. Each loop-related test has a timeout so that it fails rather than hanging.
[TestClass]
public class BreedableCalculatorEdgeCaseTests
{
    [TestMethod]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_EmptyBreedingDataReturnsOwnedPals()
    {
        var pal = FakePal(0);

        var result = Calculate(FakeBreedingDB(), [Owned(pal, PalGender.MALE)], true);

        Assert.AreEqual(pal, result.Entries.Single().Pal);
        Assert.AreEqual(0, result.NotReachable.Count);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_CircularBreedingFinishes(bool useGenderReversers)
    {
        var pals = FakePals(3);
        var db = FakeBreedingDB(
            Breed(pals[0], pals[1], pals[2]),
            Breed(pals[1], pals[2], pals[0]),
            Breed(pals[2], pals[0], pals[1])
        );

        var result = Calculate(
            db,
            [Owned(pals[0], PalGender.MALE), Owned(pals[1], PalGender.FEMALE)],
            useGenderReversers
        );

        Assert.AreEqual(3, result.Entries.Count);
        Assert.AreEqual(1, TierOf(result, pals[2]));
    }

    [TestMethod]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_ChildrenWhichAreTheirOwnParentsFinish()
    {
        var pals = FakePals(2);
        var db = FakeBreedingDB(
            Breed(pals[0], pals[1], pals[0]),
            Breed(pals[0], pals[1], pals[1]),
            Breed(pals[1], pals[0], pals[0]),
            Breed(pals[1], pals[0], pals[1])
        );

        var result = Calculate(
            db,
            [Owned(pals[0], PalGender.MALE), Owned(pals[1], PalGender.FEMALE)],
            false
        );

        Assert.AreEqual(2, result.Entries.Count);
        Assert.IsTrue(result.Entries.All(e => e.Tier == 0));
    }

    [TestMethod]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_LongestSpeciesChainReachesTheEnd()
    {
        // every generation adds exactly one species, which is the slowest way for new species to appear
        const int length = 2_000;

        var pals = FakePals(length + 1);
        var partner = pals[length];
        var db = FakeBreedingDB(Enumerable.Range(0, length - 1).Select(i => Breed(pals[i], partner, pals[i + 1])));

        var result = Calculate(db, [Owned(pals[0], PalGender.WILDCARD), Owned(partner, PalGender.WILDCARD)], false);

        Assert.AreEqual(length - 1, TierOf(result, pals[length - 1]));
    }

    [TestMethod]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_LongestGenderChainReachesTheEnd()
    {
        // every pal is owned as male only and each generation unlocks a single female, so there's a long run of
        // generations which add no new species. this is the slowest way for genders to appear
        const int length = 2_000;

        var pals = FakePals(length + 1);
        var last = pals[length - 1];
        var unlocked = pals[length];

        var breeding = Enumerable
            .Range(0, length - 1)
            .Select(i => Breed(pals[i], pals[i + 1], pals[i + 1], PalGender.FEMALE, PalGender.MALE))
            .Append(Breed(last, pals[0], unlocked, PalGender.FEMALE, PalGender.WILDCARD));

        var owned = pals
            .Take(length)
            .Select((p, i) => Owned(p, i == 0 ? PalGender.WILDCARD : PalGender.MALE))
            .ToList();

        var result = Calculate(FakeBreedingDB(breeding), owned, false);

        Assert.AreEqual(length, TierOf(result, unlocked));
        Assert.AreEqual(length + 1, result.Entries.Count);
    }

    [DataTestMethod]
    [DataRow(PalGender.OPPOSITE_WILDCARD, 3)] // treated as "any gender" by `BreedingResult.Matches`
    [DataRow(PalGender.NONE, 2)]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_UnusualRequiredGendersFollowBreedingResultMatches(PalGender requiredGender, int expected)
    {
        var pals = FakePals(3);
        var db = FakeBreedingDB(Breed(pals[0], pals[1], pals[2], requiredGender, requiredGender));
        PalInstance[] owned = [Owned(pals[0], PalGender.WILDCARD), Owned(pals[1], PalGender.WILDCARD)];

        Assert.AreEqual(expected, Calculate(db, owned, false).Entries.Count);
        Assert.AreEqual(expected, Calculate(db, owned, true).Entries.Count);
    }

    [DataTestMethod]
    [DataRow(PalGender.MALE)]
    [DataRow(PalGender.FEMALE)]
    [DataRow(PalGender.WILDCARD)]
    [DataRow(PalGender.OPPOSITE_WILDCARD)]
    [DataRow((PalGender)0)]
    [DataRow((PalGender)255)]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_AnyOwnedGenderDoesNotCrash(PalGender gender)
    {
        foreach (var useGenderReversers in new[] { true, false })
        {
            var owned = DB.Pals.Select(p => Owned(p, gender)).ToList();
            var result = Calculate(BreedingDB, owned, useGenderReversers);

            Assert.IsTrue(result.Entries.All(e => e.Tier == 0));
            Assert.AreEqual(0, result.NotReachable.Count);
        }
    }

    [TestMethod]
    public void Calculate_WildcardGenderPalsCanPair()
    {
        var result = Calculate(
            BreedingDB,
            [SolverTestScenario.Owned("Penking", PalGender.WILDCARD), SolverTestScenario.Owned("Bushi", PalGender.WILDCARD)],
            false
        );

        Assert.AreEqual(1, TierOf(result, "Sibelyx".ToPal(DB)));
    }

    [TestMethod]
    public void Calculate_OwnedPalMissingFromBreedingDataIsStillListed()
    {
        var unknown = FakePal(0);

        var result = Calculate(BreedingDB, [Owned(unknown, PalGender.MALE)], true);

        Assert.AreEqual(unknown, result.Entries.Single().Pal);
        Assert.AreEqual(DB.Pals.Count(), result.NotReachable.Count);
    }

    [TestMethod]
    public void Calculate_DuplicateDataHasNoEffect()
    {
        var pals = FakePals(3);
        var breeding = Breed(pals[0], pals[1], pals[2]);
        var owned = Enumerable.Repeat(Owned(pals[0], PalGender.WILDCARD), 1_000).Append(Owned(pals[1], PalGender.WILDCARD));

        var result = Calculate(FakeBreedingDB(breeding, breeding, breeding), owned, false);

        Assert.AreEqual(3, result.Entries.Count);
        Assert.AreEqual(1, TierOf(result, pals[2]));
    }

    [TestMethod]
    [Timeout(120_000)]
    public void Calculate_DuringSolverRunDoesNotAffectEither()
    {
        PalInstance[] owned = [
            SolverTestScenario.Owned("Penking", PalGender.MALE),
            SolverTestScenario.Owned("Bushi", PalGender.FEMALE),
            SolverTestScenario.Owned("Lamball", PalGender.FEMALE),
            SolverTestScenario.Owned("Foxparks", PalGender.MALE),
        ];

        var solver = SolverTestScenario.Solver(owned, maxThreads: 2);
        var expectedResults = SolverTestScenario.Solve(solver, "Anubis");
        var expectedEntries = new BreedableCalculator(solver.Settings).Calculate().Entries.ToList();

        var job = Task.Run(() => SolverTestScenario.Solve(solver, "Anubis"));

        var numRuns = 0;
        do
        {
            CollectionAssert.AreEqual(expectedEntries, new BreedableCalculator(solver.Settings).Calculate().Entries.ToList());
            numRuns++;
        } while (!job.IsCompleted);

        Assert.IsTrue(numRuns > 0);
        CollectionAssert.AreEquivalent(
            expectedResults.Select(r => r.BreedingEffort).ToList(),
            job.Result.Select(r => r.BreedingEffort).ToList()
        );
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_EverySingleSpeciesFinishes(bool useGenderReversers)
    {
        foreach (var pal in DB.Pals)
        {
            var result = Calculate(BreedingDB, [Owned(pal, PalGender.WILDCARD)], useGenderReversers);
            Assert.AreEqual(pal, result.Entries.Single().Pal);
        }
    }

    [TestMethod]
    [Timeout(LoopTimeoutMs)]
    public void Calculate_RandomCollectionsKeepInvariants()
    {
        var random = new Random(256);
        var palCount = DB.Pals.Count();

        for (var run = 0; run < 40; run++)
        {
            var owned = RandomPals(random, random.Next(palCount + 1));
            var ownedPals = owned.Where(p => p.Gender != PalGender.NONE).Select(p => p.Pal).ToHashSet();

            var withReversers = Calculate(BreedingDB, owned, true);
            var withoutReversers = Calculate(BreedingDB, owned, false);

            foreach (var result in new[] { withReversers, withoutReversers })
            {
                var tiers = result.Entries.ToDictionary(e => e.Pal, e => e.Tier);

                Assert.AreEqual(palCount, tiers.Count + result.NotReachable.Count);
                Assert.IsFalse(result.NotReachable.Any(tiers.ContainsKey));
                CollectionAssert.AreEquivalent(
                    ownedPals.ToList(),
                    result.Entries.Where(e => e.Tier == 0).Select(e => e.Pal).ToList()
                );

                foreach (var entry in result.Entries.Where(e => e.Tier > 0))
                {
                    Assert.AreEqual(entry.Pal, entry.Source.Child);
                    Assert.IsTrue(entry.Source.Parents.All(p => tiers[p.Pal] < entry.Tier));

                    if (entry.Parent1Instance != null) Assert.AreEqual(entry.Source.Parent1.Pal, entry.Parent1Instance.Pal);
                    if (entry.Parent2Instance != null) Assert.AreEqual(entry.Source.Parent2.Pal, entry.Parent2Instance.Pal);

                    if (result == withoutReversers && entry.Parent1Instance != null && entry.Parent2Instance != null)
                    {
                        Assert.AreEqual(
                            PalGender.WILDCARD,
                            (entry.Parent1Instance.Gender & PalGender.WILDCARD) | (entry.Parent2Instance.Gender & PalGender.WILDCARD)
                        );
                    }
                }
            }

            // nothing was missed: any pair of reachable pals only ever produces another reachable pal, one
            // generation later at most
            var reachable = withReversers.Entries.ToDictionary(e => e.Pal, e => e.Tier);
            foreach (var breeding in BreedingDB.Breeding)
            {
                if (breeding.Parent1.Pal == breeding.Parent2.Pal) continue;
                if (!reachable.TryGetValue(breeding.Parent1.Pal, out var tier1)) continue;
                if (!reachable.TryGetValue(breeding.Parent2.Pal, out var tier2)) continue;

                Assert.IsTrue(reachable[breeding.Child] <= Math.Max(tier1, tier2) + 1);
            }

            // gender reversers can only ever help
            foreach (var entry in withoutReversers.Entries)
                Assert.IsTrue(reachable[entry.Pal] <= entry.Tier);
        }
    }

    private const int LoopTimeoutMs = 30_000;

    private static readonly PalDB DB = SolverTestScenario.DB;
    private static readonly PalBreedingDB BreedingDB = PalBreedingDB.LoadEmbedded(DB);

    private static readonly PalGender[] AllGenders = [
        PalGender.MALE,
        PalGender.FEMALE,
        PalGender.WILDCARD,
        PalGender.OPPOSITE_WILDCARD,
        PalGender.NONE,
    ];

    private static Pal FakePal(int index) => new()
    {
        Id = new PalId { PalDexNo = 100_000 + index },
        Name = $"Fake {index}",
        InternalName = $"Fake{index}",
        InternalIndex = 100_000 + index,
    };

    private static List<Pal> FakePals(int count) => Enumerable.Range(0, count).Select(FakePal).ToList();

    private static PalInstance Owned(Pal pal, PalGender gender) => new() { Pal = pal, Gender = gender };

    private static BreedingResult Breed(
        Pal parent1,
        Pal parent2,
        Pal child,
        PalGender parent1Gender = PalGender.WILDCARD,
        PalGender parent2Gender = PalGender.WILDCARD
    ) => new()
    {
        Parent1 = new GenderedPal(parent1, parent1Gender),
        Parent2 = new GenderedPal(parent2, parent2Gender),
        Child = child,
    };

    private static PalBreedingDB FakeBreedingDB(params IEnumerable<BreedingResult> breeding) =>
        new(DB) { Breeding = breeding.ToList() };

    private static BreedableResult Calculate(PalBreedingDB db, IEnumerable<PalInstance> ownedPals, bool useGenderReversers) =>
        new BreedableCalculator(
            SolverTestScenario.Solver(ownedPals, breedingDB: db, useGenderReversers: useGenderReversers).Settings
        ).Calculate();

    private static int TierOf(BreedableResult result, Pal pal) => result.Entries.Single(e => e.Pal == pal).Tier;

    private static List<PalInstance> RandomPals(Random random, int count) =>
        DB.Pals
            .OrderBy(p => random.Next())
            .Take(count)
            .Select(p => Owned(p, AllGenders[random.Next(AllGenders.Length)]))
            .ToList();
}
