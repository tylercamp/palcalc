using PalCalc.Model;

namespace PalCalc.Solver.PalReference.Properties;

public enum AttackInheritanceMode
{
    Baseline,
    Normal,
    InheritAll,
}

/// <summary>
/// Concrete attack instructions for one materialized breeding edge.
/// </summary>
public sealed record MaterializedAttackInheritance(
    AttackInheritanceMode Mode,
    IReadOnlyList<ActiveSkill> Parent1Loadout,
    IReadOnlyList<ActiveSkill> Parent2Loadout,
    IReadOnlyList<ActiveSkill> InheritedAttacks,
    IReadOnlyList<ActiveSkill> ChildMasteredAttacks,
    int SpecialCakes,
    float AttackProbability
);
