using PalCalc.Model;

namespace PalCalc.Solver.PalReference.Properties;

public enum AttackInheritanceMode
{
    Baseline,
    Normal,
    InheritAll,
}

/// <summary>
/// A specific set of attack-inheritance choices. Derived from an `AttackProfile` using
/// a `AttackResultMaterializer`.
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
