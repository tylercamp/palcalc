using PalCalc.Solver.Probabilities;

namespace PalCalc.Solver.Tests.Probabilities;

[TestClass]
public class AttackInheritanceTests
{
    [DataTestMethod]
    [DataRow(false, false, false, false, 0f)]
    [DataRow(true, false, false, false, 0.5f)]
    [DataRow(false, true, false, false, 0.5f)]
    [DataRow(true, true, false, false, 1f)]
    [DataRow(true, false, false, true, 1f)]
    [DataRow(false, true, true, false, 1f)]
    public void ProbabilityUsesBestLegalOneAttackLoadouts(
        bool parent1HasTarget,
        bool parent2HasTarget,
        bool parent1HasNeutralAttack,
        bool parent2HasNeutralAttack,
        float expected
    ) => Assert.AreEqual(
        expected,
        Attacks.ProbabilityInheritedTargetAttack(
            parent1HasTarget,
            parent2HasTarget,
            parent1HasNeutralAttack,
            parent2HasNeutralAttack
        )
    );
}
