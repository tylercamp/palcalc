using System.Numerics;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackProfileComposerTests
{
    private static readonly Pal Child = "Wixen Noct".ToPal(SolverTestScenario.DB);

    [TestMethod]
    public void Compose_BaselineContainsOnlyInnateTargets()
    {
        var innate = Child.Level1ActiveSkills(SolverTestScenario.DB).First();

        var profile = Compose([innate], Entry(0), Entry(0), cakes: 0);

        Assert.AreEqual(new AttackProfileEntry(1, 0), profile.Entries.Single());
    }

    [TestMethod]
    public void Compose_NormalInheritanceAddsExactlyOneNonInnateAttack()
    {
        var profile = Compose(
            Attacks(2),
            Entry(0b11),
            Entry(0),
            cakes: 0
        );

        CollectionAssert.AreEquivalent(
            new byte[] { 0, 0b01, 0b10 },
            profile.Entries.Select(entry => entry.LearnedTargetMask).ToArray()
        );
        Assert.IsTrue(profile.Entries.All(entry => entry.TotalSpecialCakes == 0));
    }

    [TestMethod]
    public void Compose_NormalAvailabilityIgnoresAttackProbabilityAndNoopState()
    {
        var attack = Attacks(1);
        var context = Context(attack);
        var settings = Settings(cakes: 0);
        var parent1 = new AttackProfile(Entry(1));
        var parent2 = new AttackProfile(Entry(0));

        var normal = new AttackProfileComposer(context, settings)
            .Compose(Child, Reference(parent1), Reference(parent2), 1, 1);
        var lowProbability = new AttackProfileComposer(context, settings)
            .Compose(Child, Reference(parent1, neutral: true), Reference(parent2), 0.01f, 1);

        Assert.AreEqual(normal, lowProbability);
        Assert.IsTrue(normal.Contains(1));
    }

    [TestMethod]
    public void Compose_ReusesProfileForTheSameRoundedCakeCost()
    {
        var context = Context(Attacks(1));
        var settings = Settings(cakes: 100);
        var parent1 = Reference(new AttackProfile(Entry(1)));
        var parent2 = Reference(new AttackProfile(Entry(0)));
        var composer = new AttackProfileComposer(context, settings);

        var first = composer.Compose(Child, parent1, parent2, 0.5f, 0.5f);
        var second = composer.Compose(Child, parent1, parent2, 0.51f, 0.5f);

        Assert.AreSame(first.Entries, second.Entries);
    }

    [TestMethod]
    public void Compose_NonInheritableTargetsNeverTransfer()
    {
        var nonInheritable = SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit && !Child.Level1AttackInternalIds.Contains(attack.InternalName));
        var inheritable = Attacks(1).Single();

        var profile = Compose(
            [nonInheritable, inheritable],
            Entry(0b11),
            Entry(0),
            cakes: 0
        );

        CollectionAssert.AreEquivalent(
            new byte[] { 0, 0b10 },
            profile.Entries.Select(entry => entry.LearnedTargetMask).ToArray()
        );
    }

    [DataTestMethod]
    [DataRow(9, true)]
    [DataRow(8, false)]
    public void Compose_CakeCostIncludesParentsAndHonorsLimit(
        int cakeLimit,
        bool expected
    )
    {
        var profile = Compose(
            Attacks(6),
            Entry(0b000111, cakes: 2),
            Entry(0b111000, cakes: 3),
            cakes: cakeLimit,
            passivesProbability: 0.5f,
            ivsProbability: 0.5f
        );

        Assert.AreEqual(expected, profile.Contains(0b111111));
        if (expected)
            Assert.AreEqual(9, profile.Entries.Single(entry => entry.LearnedTargetMask == 0b111111)
                .TotalSpecialCakes);
    }

    [TestMethod]
    public void Compose_CategoryChampionsMatchBruteForceNormalAvailability()
    {
        var attacks = Attacks(2);
        var settings = Settings(cakes: 0);
        var context = Context(attacks);
        var parent1 = new AttackProfile(
            Entry(0b00),
            Entry(0b01),
            Entry(0b10),
            Entry(0b11)
        );
        var parent2 = new AttackProfile(
            Entry(0b00),
            Entry(0b01),
            Entry(0b10)
        );
        var parent1Reference = Reference(parent1);
        var parent2Reference = Reference(parent2);

        var optimized = new AttackProfileComposer(context, settings)
            .Compose(Child, parent1Reference, parent2Reference, 1, 1);
        var bruteForceEntries = new List<AttackProfileEntry>();
        var innateMask = context.StateOf(Child).Level1TargetMask;
        var inheritableMask = context.InheritableTargetMask;
        foreach (var first in parent1.Entries)
            foreach (var second in parent2.Entries)
            {
                bruteForceEntries.Add(new(innateMask, first.TotalSpecialCakes + second.TotalSpecialCakes));
                var available = (byte)((first.LearnedTargetMask | second.LearnedTargetMask) & inheritableMask);
                for (var bit = 1; bit <= context.FullTargetMask; bit <<= 1)
                    if ((available & bit) != 0 && (innateMask & bit) == 0)
                        bruteForceEntries.Add(new((byte)(innateMask | bit), first.TotalSpecialCakes + second.TotalSpecialCakes));
            }

        var expected = AttackProfileReducer.Reduce(bruteForceEntries.ToArray());

        Assert.AreEqual(expected, optimized);
    }

    [TestMethod]
    public void CakeMasks_MatchBruteForceForEveryParentMaskPair()
    {
        Span<ushort> actualLoadouts = stackalloc ushort[64];
        for (byte parent1Mask = 0; parent1Mask < 64; parent1Mask++)
            for (byte parent2Mask = 0; parent2Mask < 64; parent2Mask++)
            {
                var actualCount = AttackProfileComposer.EnumerateCakeMasks(
                    parent1Mask, parent2Mask, actualLoadouts
                );
                var actualMasks = new HashSet<byte>();
                for (var i = 0; i < actualCount; i++)
                {
                    var parent1Loadout = (byte)(actualLoadouts[i] >> 8);
                    var parent2Loadout = (byte)actualLoadouts[i];
                    Assert.AreEqual(0, parent1Loadout & ~parent1Mask);
                    Assert.AreEqual(0, parent2Loadout & ~parent2Mask);
                    Assert.IsTrue(BitOperations.PopCount((uint)parent1Loadout) <= 3);
                    Assert.IsTrue(BitOperations.PopCount((uint)parent2Loadout) <= 3);
                    Assert.IsTrue(actualMasks.Add((byte)(parent1Loadout | parent2Loadout)));
                }

                CollectionAssert.AreEquivalent(
                    BruteForceMaximalCakeMasks(parent1Mask, parent2Mask),
                    actualMasks.ToArray(),
                    $"Parent masks: {parent1Mask}, {parent2Mask}"
                );
            }
    }

    private static AttackProfile Compose(
        IEnumerable<ActiveSkill> attacks,
        AttackProfileEntry parent1,
        AttackProfileEntry parent2,
        int? cakes,
        float passivesProbability = 1,
        float ivsProbability = 1
    )
    {
        var settings = Settings(cakes: cakes);
        return new AttackProfileComposer(Context(attacks), settings)
            .Compose(
                Child,
                Reference(new(parent1)),
                Reference(new(parent2)),
                passivesProbability,
                ivsProbability
            );
    }

    private static AttackTargetContext Context(IEnumerable<ActiveSkill> attacks) =>
        new(new PalSpecifier { RequiredAttacks = attacks.ToList() }, SolverTestScenario.DB);

    private static BreedingSolverSettings Settings(int? cakes) =>
        SolverTestScenario.Solver([], maxSpecialCakes: cakes).Settings;

    private static OwnedPalReference Reference(AttackProfile profile, bool neutral = false) =>
        new(
            SolverTestScenario.Owned("Katress", PalGender.MALE),
            [],
            new IV_Set(),
            attackProfile: neutral
                ? new AttackProfile(true, profile.Entries.ToArray())
                : profile
        );

    private static AttackProfileEntry Entry(byte mask, int cakes = 0) => new(mask, cakes);

    private static ActiveSkill[] Attacks(int count) => SolverTestScenario.DB.ActiveSkills
        .Where(attack => attack.CanInherit && !Child.Level1AttackInternalIds.Contains(attack.InternalName))
        .Take(count)
        .ToArray();

    private static int BitCount(byte mask)
    {
        var count = 0;
        for (; mask != 0; mask >>= 1)
            count += mask & 1;
        return count;
    }

    private static byte[] BruteForceMaximalCakeMasks(byte parent1Mask, byte parent2Mask)
    {
        var feasible = new HashSet<byte>();
        for (var parent1Loadout = 0; parent1Loadout < 64; parent1Loadout++)
        {
            if ((parent1Loadout & ~parent1Mask) != 0 || BitCount((byte)parent1Loadout) > 3)
                continue;
            for (var parent2Loadout = 0; parent2Loadout < 64; parent2Loadout++)
                if ((parent2Loadout & ~parent2Mask) == 0 && BitCount((byte)parent2Loadout) <= 3)
                    feasible.Add((byte)(parent1Loadout | parent2Loadout));
        }

        return feasible
            .Where(mask => !feasible.Any(other => other != mask && (other & mask) == mask))
            .ToArray();
    }
}
