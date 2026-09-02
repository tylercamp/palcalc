using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Utils;

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

        Assert.AreEqual(1, profile.Entries.Count);
        Assert.AreEqual(1, profile.Entries.Single().LearnedTargetMask);
        Assert.IsFalse(profile.Entries.Single().SelfUsesSpecialCake);
    }

    [TestMethod]
    public void Compose_NormalInheritanceAddsExactlyOneNonInnateAttack()
    {
        var attacks = Attacks(2);
        var profile = Compose(attacks, Entry(0b11), Entry(0), cakes: 0);

        CollectionAssert.AreEquivalent(
            new byte[] { 0, 0b01, 0b10 },
            profile.Entries.Select(entry => entry.LearnedTargetMask).ToArray()
        );
    }

    [TestMethod]
    public void Compose_NormalInheritanceUsesExpectedCarrierProbabilities()
    {
        var attack = Attacks(1);
        var bothParents = Choices(attack, Entry(1), Entry(1), cakes: 0);
        var neutralMate = Choices(attack, Entry(1), Entry(0), cakes: 0, parent2Neutral: true);
        var inheritableFiller = Choices(attack, Entry(1), Entry(0), cakes: 0);

        Assert.AreEqual(1f, Normal(bothParents).AttackProbability);
        Assert.AreEqual(1f, Normal(neutralMate).AttackProbability);
        Assert.AreEqual(0.5f, Normal(inheritableFiller).AttackProbability);
    }

    [TestMethod]
    public void Compose_NonInheritableTargetsNeverTransfer()
    {
        var nonInheritable = SolverTestScenario.DB.ActiveSkills.First(attack => !attack.CanInherit &&
            !Child.Level1AttackInternalIds.Contains(attack.InternalName));
        var inheritable = Attacks(1).Single();
        var profile = Compose([nonInheritable, inheritable], Entry(0b11), Entry(0), cakes: 0);

        CollectionAssert.AreEquivalent(
            new byte[] { 0, 0b10 },
            profile.Entries.Select(entry => entry.LearnedTargetMask).ToArray()
        );
        Assert.IsTrue(Choices([nonInheritable, inheritable], Entry(0b11), Entry(0), cakes: null)
            .Where(choice => choice.Mode == AttackCompositionMode.InheritAll)
            .All(choice => choice.ChildEntry.LearnedTargetMask == 0b10));
    }

    [TestMethod]
    public void Compose_CakeAddsInnateTargetsWithoutUsingParentSlots()
    {
        var innate = Child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var inherited = Attacks(4);
        var choices = Choices([innate, .. inherited], Entry(0b11110), Entry(0), cakes: null)
            .Where(choice => choice.Mode == AttackCompositionMode.InheritAll)
            .ToArray();

        Assert.AreEqual(4, choices.Length);
        Assert.IsTrue(choices.All(choice => (choice.ChildEntry.LearnedTargetMask & 1) != 0));
        Assert.IsTrue(choices.All(choice => BitCount(choice.Parent1TargetMask) == 3));
    }

    [TestMethod]
    public void Compose_CakeCombinesAndDeduplicatesParentAttacks()
    {
        var attacks = Attacks(3);
        var choices = Choices(attacks, Entry(0b011), Entry(0b110), cakes: null);
        var cake = choices.Single(choice => choice.Mode == AttackCompositionMode.InheritAll);

        Assert.AreEqual(0b111, cake.ChildEntry.LearnedTargetMask);
        Assert.AreEqual(0b011, cake.Parent1TargetMask);
        Assert.AreEqual(0b110, cake.Parent2TargetMask);
    }

    [TestMethod]
    public void Compose_CakeRespectsThreeSlotsPerParent()
    {
        var attacks = Attacks(6);
        var oneParent = Choices(attacks.Take(4), Entry(0b1111), Entry(0), cakes: null)
            .Where(choice => choice.Mode == AttackCompositionMode.InheritAll)
            .ToArray();
        var splitParents = Choices(attacks, Entry(0b000111), Entry(0b111000), cakes: null)
            .Where(choice => choice.Mode == AttackCompositionMode.InheritAll)
            .ToArray();
        var overlappingParents = Choices(attacks.Take(4), Entry(0b1111), Entry(0b1000), cakes: null)
            .Single(choice => choice.Mode == AttackCompositionMode.InheritAll);

        Assert.AreEqual(4, oneParent.Length);
        Assert.IsTrue(oneParent.All(choice => BitCount(choice.ChildEntry.LearnedTargetMask) == 3));
        Assert.AreEqual(1, splitParents.Length);
        Assert.AreEqual(0b111111, splitParents.Single().ChildEntry.LearnedTargetMask);
        Assert.AreEqual(0b1111, overlappingParents.ChildEntry.LearnedTargetMask);
        Assert.IsTrue(BitCount(overlappingParents.Parent1TargetMask) <= 3);
        Assert.IsTrue(BitCount(overlappingParents.Parent2TargetMask) <= 3);
    }

    [TestMethod]
    public void Compose_CakeCountsAndLimitsAreAppliedDuringComposition()
    {
        var attack = Attacks(1);
        var parent1 = Entry(1, cakes: 2);
        var parent2 = Entry(0, cakes: 3);
        var unlimited = Choices(attack, parent1, parent2, cakes: null, passivesProbability: 0.5f)
            .Single(choice => choice.Mode == AttackCompositionMode.InheritAll);
        var limited = Compose(attack, parent1, parent2, cakes: 6, passivesProbability: 0.5f);
        var disabled = Choices(attack, parent1, parent2, cakes: 0, passivesProbability: 0.5f);

        Assert.AreEqual(7, unlimited.ChildEntry.TotalSpecialCakes);
        Assert.IsFalse(limited.Entries.Any(entry => entry.SelfUsesSpecialCake));
        Assert.IsFalse(disabled.Any(choice => choice.Mode == AttackCompositionMode.InheritAll));
    }

    [TestMethod]
    public void Compose_DominanceRetainsTimeCakeTradeoffs()
    {
        var attack = Attacks(1);
        var profile = Compose(
            attack,
            Entry(1, cakes: 0, effortMinutes: 20),
            Entry(0, cakes: 0, effortMinutes: 0),
            cakes: null
        );

        Assert.IsTrue(profile.Entries.Any(entry => entry.LearnedTargetMask == 1 && !entry.SelfUsesSpecialCake));
        Assert.IsTrue(profile.Entries.Any(entry => entry.LearnedTargetMask == 1 && entry.SelfUsesSpecialCake));
    }

    [TestMethod]
    public void Compose_RemovesEntriesOverTheEffortLimit()
    {
        var settings = Settings(cakes: 0, maxEffort: TimeSpan.Zero);
        var composer = new AttackProfileComposer(Context(Attacks(1)), settings, new ObjectPoolFactory());

        Assert.AreEqual(0, composer.Compose(Child, Reference(new(Entry(0))), Reference(new(Entry(0))), 1, 1).Entries.Count);
    }

    [TestMethod]
    public void Compose_MultipleFarmsUsesMaxOnlyForTwoBredParents()
    {
        var attack = Attacks(1);
        var gameSettings = new GameSettings { MultipleBreedingFarms = true };
        var settings = Settings(cakes: 0, gameSettings: gameSettings);
        var context = Context(attack);
        var profile1 = new AttackProfile(Entry(0, effortMinutes: 10));
        var profile2 = new AttackProfile(Entry(0, effortMinutes: 20));
        var owned1 = Reference(profile1);
        var owned2 = Reference(profile2);
        var bred1 = BredReference(settings, profile1);
        var bred2 = BredReference(settings, profile2);
        var composer = new AttackProfileComposer(context, settings, new ObjectPoolFactory());
        var self = BredPalReferenceEffort.CalculateSelfBreedingEffort(gameSettings, Child, 1, 1, 1);

        Assert.AreEqual(TimeSpan.FromMinutes(30) + self, composer.Compose(Child, owned1, owned2, 1, 1).Entries.Single().BreedingEffort);
        Assert.AreEqual(TimeSpan.FromMinutes(20) + self, composer.Compose(Child, bred1, bred2, 1, 1).Entries.Single().BreedingEffort);
    }

    private static AttackProfile Compose(
        IEnumerable<ActiveSkill> attacks,
        AttackProfileEntry parent1,
        AttackProfileEntry parent2,
        int? cakes,
        float passivesProbability = 1
    )
    {
        var settings = Settings(cakes: cakes);
        return new AttackProfileComposer(Context(attacks), settings, new ObjectPoolFactory())
            .Compose(Child, Reference(new(parent1)), Reference(new(parent2)), passivesProbability, 1);
    }

    private static IReadOnlyList<AttackCompositionChoice> Choices(
        IEnumerable<ActiveSkill> attacks,
        AttackProfileEntry parent1,
        AttackProfileEntry parent2,
        int? cakes,
        bool parent2Neutral = false,
        float passivesProbability = 1
    )
    {
        var settings = Settings(cakes: cakes);
        return new AttackProfileComposer(Context(attacks), settings, new ObjectPoolFactory())
            .EnumerateChoices(Child, Reference(new(parent1)), Reference(new(parent2), parent2Neutral), passivesProbability, 1);
    }

    private static AttackCompositionChoice Normal(IEnumerable<AttackCompositionChoice> choices) =>
        choices.Single(choice => choice.Mode == AttackCompositionMode.Normal);

    private static AttackTargetContext Context(IEnumerable<ActiveSkill> attacks) =>
        new(new PalSpecifier { RequiredAttacks = attacks.ToList() }, SolverTestScenario.DB);

    private static BreedingSolverSettings Settings(
        int? cakes,
        GameSettings? gameSettings = null,
        TimeSpan? maxEffort = null
    ) => SolverTestScenario.Solver(
        [], maxSpecialCakes: cakes, gameSettings: gameSettings, maxEffort: maxEffort
    ).Settings;

    private static OwnedPalReference Reference(AttackProfile profile, bool neutral = false) =>
        new(
            SolverTestScenario.Owned("Katress", PalGender.MALE),
            [],
            new IV_Set(),
            attackProfile: neutral
                ? new AttackProfile(true, profile.Entries.ToArray())
                : profile
        );

    private static BredPalReference BredReference(BreedingSolverSettings settings, AttackProfile profile)
    {
        var first = Reference(new AttackProfile(Entry(0)));
        var second = new OwnedPalReference(
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE), [], new IV_Set(),
            attackProfile: AttackProfile.Inactive
        );
        return new BredPalReference(
            settings.GameSettings, Child, first, second, [], 1, new IV_Set(), 1,
            attackProfile: profile,
            materializedAttackInheritance: null,
            avgRequiredBreedings: null,
            gender: PalGender.WILDCARD
        );
    }

    private static AttackProfileEntry Entry(byte mask, int cakes = 0, int effortMinutes = 0) =>
        new(mask, cakes, TimeSpan.FromMinutes(effortMinutes), 0, false);

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
}
