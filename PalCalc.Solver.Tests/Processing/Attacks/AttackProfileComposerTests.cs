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

        var profile = Compose([innate], (byte)0, (byte)0, policy: SpecialCakePolicy.Never);

        Assert.AreEqual((byte)1, profile.Masks.Single());
    }

    [TestMethod]
    public void Compose_NormalInheritanceAddsExactlyOneNonInnateAttack()
    {
        var profile = Compose(
            Attacks(2),
            (byte)0b11,
            (byte)0,
            policy: SpecialCakePolicy.Never
        );

        CollectionAssert.AreEquivalent(
            new byte[] { 0, 0b01, 0b10 },
            profile.Masks.ToArray()
        );
    }

    [TestMethod]
    public void Compose_NormalAvailabilityIgnoresParentNoopState()
    {
        var attack = Attacks(1);
        var context = Context(attack);
        var settings = Settings(policy: SpecialCakePolicy.Never);
        var parent1 = new AttackProfile((byte)1);
        var parent2 = new AttackProfile((byte)0);

        var normal = Compose(
            new AttackProfileComposer(context, settings),
            Child, Reference(parent1), Reference(parent2), BreedingMethod.Ordinary
        );
        var withNoopParent = Compose(
            new AttackProfileComposer(context, settings),
            Child, Reference(parent1, neutral: true), Reference(parent2), BreedingMethod.Ordinary
        );

        Assert.AreEqual(normal, withNoopParent);
        Assert.IsTrue(normal.Contains(1));
    }

    [TestMethod]
    public void Compose_NonInheritableTargetsNeverTransfer()
    {
        var nonInheritable = SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit && !Child.Level1AttackInternalIds.Contains(attack.InternalName));
        var inheritable = Attacks(1).Single();

        var profile = Compose(
            [nonInheritable, inheritable],
            (byte)0b11,
            (byte)0,
            policy: SpecialCakePolicy.Never
        );

        CollectionAssert.AreEquivalent(
            new byte[] { 0, 0b10 },
            profile.Masks.ToArray()
        );
    }

    [TestMethod]
    public void Compose_CachesFeasibilitySeparatelyByMethod()
    {
        var attacks = Attacks(2);
        var context = Context(attacks);
        var settings = Settings(SpecialCakePolicy.AttackInheritanceOnly);
        var parent1 = Reference(new AttackProfile((byte)0b01));
        var parent2 = Reference(new AttackProfile((byte)0b10));

        var composer = new AttackProfileComposer(context, settings);
        var impossible = composer.Compose(Child, parent1, parent2, BreedingMethod.Ordinary);
        var possible = composer.Compose(Child, parent1, parent2, BreedingMethod.SpecialCake);

        Assert.IsFalse(impossible.Contains(0b11));
        Assert.IsTrue(possible.Contains(0b11));
    }

    [TestMethod]
    [DataRow(SpecialCakePolicy.Never, false)]
    [DataRow(SpecialCakePolicy.AttackInheritanceOnly, true)]
    [DataRow(SpecialCakePolicy.AttacksAndPassives, true)]
    public void Compose_CakeOnlyOutcomeFollowsPolicy(SpecialCakePolicy policy, bool expected)
    {
        var context = Context(Attacks(2));
        var composer = new AttackProfileComposer(context, Settings(policy));
        var profile = Compose(
            composer, Child,
            Reference(new AttackProfile((byte)1)),
            Reference(new AttackProfile((byte)2)),
            BreedingMethod.SpecialCake
        );

        Assert.AreEqual(expected, profile.Contains(3));
        Assert.AreEqual(expected ? 1 : 0, profile.Masks.Count());
        if (expected)
            Assert.AreEqual((byte)3, profile.Masks.Single());
    }

    [TestMethod]
    public void Compose_NormalMasksMatchBruteForceAvailability()
    {
        var attacks = Attacks(2);
        var settings = Settings(policy: SpecialCakePolicy.Never);
        var context = Context(attacks);
        var parent1 = new AttackProfile((byte)0b00, (byte)0b01, (byte)0b10, (byte)0b11);
        var parent2 = new AttackProfile((byte)0b00, (byte)0b01, (byte)0b10);

        var optimized = Compose(
            new AttackProfileComposer(context, settings),
            Child, Reference(parent1), Reference(parent2), BreedingMethod.Ordinary
        );
        var expected = BruteForceNormalProfile(context, parent1, parent2);

        Assert.AreEqual(expected, optimized);
    }

    [TestMethod]
    public void Compose_CakesCannotBeIntroducedForPassivesOrOneNonInnateAttack()
    {
        var innate = Child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var context = Context([innate, Attacks(1).Single()]);
        var profile = Compose(
            new AttackProfileComposer(context, Settings(SpecialCakePolicy.AttackInheritanceOnly)), Child,
            Reference(new AttackProfile((byte)3)), Reference(new AttackProfile((byte)0)),
            BreedingMethod.SpecialCake
        );
        Assert.AreEqual(0, profile.Masks.Count());
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
        byte parent1,
        byte parent2,
        SpecialCakePolicy policy
    )
    {
        var settings = Settings(policy);
        return Compose(
            new AttackProfileComposer(Context(attacks), settings),
            Child, Reference(new(parent1)), Reference(new(parent2)),
            policy == SpecialCakePolicy.Never ? BreedingMethod.Ordinary : BreedingMethod.SpecialCake
        );
    }

    private static AttackProfile Compose(
        AttackProfileComposer composer,
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        BreedingMethod method
    ) => composer.Compose(child, parent1, parent2, method);

    private static AttackTargetContext Context(IEnumerable<ActiveSkill> attacks) =>
        new(new PalSpecifier { RequiredAttacks = attacks.ToList() }, SolverTestScenario.DB);

    private static BreedingSolverSettings Settings(SpecialCakePolicy policy) =>
        SolverTestScenario.Solver([], specialCakePolicy: policy).Settings;

    private static OwnedPalReference Reference(AttackProfile profile, bool neutral = false) =>
        new(
            SolverTestScenario.Owned("Katress", PalGender.MALE),
            [],
            new IV_Set(),
            attackProfile: neutral
                ? new AttackProfile(true, profile.Masks.ToArray())
                : profile
        );

    private static ActiveSkill[] Attacks(int count) => SolverTestScenario.DB.ActiveSkills
        .Where(attack => attack.CanInherit && !Child.Level1AttackInternalIds.Contains(attack.InternalName))
        .Take(count)
        .ToArray();

    private static AttackProfile BruteForceNormalProfile(
        AttackTargetContext context,
        AttackProfile parent1,
        AttackProfile parent2
    )
    {
        var masks = new HashSet<byte>();
        var innate = context.StateOf(Child).Level1TargetMask;
        foreach (var first in parent1.Masks)
            foreach (var second in parent2.Masks)
            {
                masks.Add(innate);
                var available = (first | second) & context.InheritableTargetMask;
                for (var bit = 1; bit <= context.FullTargetMask; bit <<= 1)
                    if ((available & bit) != 0)
                        masks.Add((byte)(innate | bit));
            }

        return new AttackProfile(context.StateOf(Child).HasNooplLevel1Attack, masks.ToArray());
    }
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
