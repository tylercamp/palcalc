using PalCalc.Model;

namespace PalCalc.Solver.PalReference.Properties;

public enum AttackInheritanceMode
{
    Baseline, // No target attacks are inherited; only the child's innate level-1 target attacks apply
    Normal, // Attack inheritance is relevant, use normal 1-attack
    InheritAll, // Attack inheritance will inherit all equipped from each parent (Special Cakes)
}

/// <summary>
/// A specific set of attack-inheritance choices. Derived from an `AttackProfile` using
/// an `AttackResultMaterializer`.
/// </summary>
public sealed record MaterializedAttackInheritance(
    AttackInheritanceMode Mode,
    IReadOnlyList<ActiveSkill> Parent1Loadout,
    IReadOnlyList<ActiveSkill> Parent2Loadout,
    IReadOnlyList<ActiveSkill> InheritedAttacks,
    IReadOnlyList<ActiveSkill> ChildLearnedAttacks,
    int SpecialCakes,
    float AttackProbability
);
