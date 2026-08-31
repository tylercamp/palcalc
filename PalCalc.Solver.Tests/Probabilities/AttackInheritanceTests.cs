using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Probabilities;

namespace PalCalc.Solver.Tests.Probabilities;

[TestClass]
public class AttackInheritanceTests
{
    private static PalDB DB => SolverTestScenario.DB;
    private static ActiveSkill Target => DB.ActiveSkills.First(attack => attack.CanInherit);
    private static ActiveSkill Other => DB.ActiveSkills.First(attack => attack.CanInherit && attack != Target);
    private static ActiveSkill Neutral => DB.ActiveSkills.First(attack => !attack.CanInherit);

    [TestMethod]
    public void NoTargetUsesLevelOneFallback()
    {
        var neutral = Attacks.InheritanceOutcome(DB, null, Child(Neutral, Target), Parent(Target), Parent(Other));
        var inheritable = Attacks.InheritanceOutcome(DB, null, Child(Target), Parent(null), Parent(null));

        Assert.AreSame(Neutral, neutral.ActualAttack);
        Assert.IsNull(neutral.EffectiveAttack);
        Assert.AreEqual(1, neutral.Probability);
        Assert.IsInstanceOfType<RandomActiveSkill>(inheritable.ActualAttack);
        Assert.AreSame(inheritable.ActualAttack, inheritable.EffectiveAttack);
        Assert.AreEqual(1, inheritable.Probability);
    }

    [TestMethod]
    public void LevelOneTargetIsGuaranteedEvenWhenNonInheritable()
    {
        var outcome = Attacks.InheritanceOutcome(DB, Neutral, Child(Neutral), Parent(null), Parent(null));

        Assert.AreSame(Neutral, outcome.ActualAttack);
        Assert.AreSame(Neutral, outcome.EffectiveAttack);
        Assert.AreEqual(1, outcome.Probability);
    }

    [TestMethod]
    public void TargetProbabilityUsesDistinctEligibleParentPool()
    {
        var withNeutral = Attacks.InheritanceOutcome(DB, Target, Child(Other), Parent(Target), Parent(Neutral));
        var withOther = Attacks.InheritanceOutcome(DB, Target, Child(Other), Parent(Target), Parent(Other));
        var onBoth = Attacks.InheritanceOutcome(DB, Target, Child(Other), Parent(Target), Parent(Target));

        Assert.AreEqual(1, withNeutral.Probability);
        Assert.AreEqual(0.5f, withOther.Probability);
        Assert.AreEqual(1, onBoth.Probability);
        Assert.AreSame(Target, withOther.ActualAttack);
        Assert.AreSame(Target, withOther.EffectiveAttack);
    }

    [TestMethod]
    public void MissingTargetCollapsesIrrelevantInheritanceToAnonymousAttack()
    {
        var sameConcrete = Attacks.InheritanceOutcome(DB, Target, Child(Other), Parent(Other), Parent(Other));
        var firstRandom = new RandomActiveSkill();
        var secondRandom = new RandomActiveSkill();
        var distinctAnonymous = Attacks.InheritanceOutcome(
            DB,
            firstRandom,
            Child(),
            Parent(firstRandom),
            Parent(secondRandom)
        );

        Assert.AreEqual(1, sameConcrete.Probability);
        Assert.IsInstanceOfType<RandomActiveSkill>(sameConcrete.ActualAttack);
        Assert.AreSame(sameConcrete.ActualAttack, sameConcrete.EffectiveAttack);
        Assert.AreEqual(0.5f, distinctAnonymous.Probability);
        Assert.AreSame(firstRandom, distinctAnonymous.ActualAttack);
        Assert.AreSame(firstRandom, distinctAnonymous.EffectiveAttack);
    }

    [TestMethod]
    public void NonInheritableTargetCannotTransfer()
    {
        var outcome = Attacks.InheritanceOutcome(DB, Neutral, Child(Target), Parent(Neutral), Parent(null));

        Assert.AreEqual(1, outcome.Probability);
        Assert.AreNotSame(Neutral, outcome.ActualAttack);
        Assert.IsInstanceOfType<RandomActiveSkill>(outcome.EffectiveAttack);
    }

    [TestMethod]
    public void EmptyEligiblePoolUsesNeutralLevelOneAttack()
    {
        var outcome = Attacks.InheritanceOutcome(DB, Target, Child(Neutral), Parent(Neutral), Parent(null));

        Assert.AreSame(Neutral, outcome.ActualAttack);
        Assert.IsNull(outcome.EffectiveAttack);
        Assert.AreEqual(1, outcome.Probability);
    }

    private static Pal Child(params ActiveSkill[] level1Attacks) =>
        new()
        {
            Level1AttackInternalIds = level1Attacks.Select(attack => attack.InternalName).ToList(),
        };

    private static IPalReference Parent(ActiveSkill? attack) =>
        new OwnedPalReference(
            SolverTestScenario.Owned("Katress", PalGender.MALE),
            [],
            new IV_Set(),
            actualAttack: attack,
            effectiveAttack: attack,
            attackProfile: AttackProfile.Inactive,
            hasNeutralAttack: false
        );
}
