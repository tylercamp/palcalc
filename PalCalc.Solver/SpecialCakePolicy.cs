namespace PalCalc.Solver;

/// <summary>Controls how Special Cakes may be used during the solver process</summary>
public enum SpecialCakePolicy
{
    Never,
    /// <summary>Use cakes only to inherit multiple requested, non-innate attacks.</summary>
    AttackInheritanceOnly,
    /// <summary>Also consider cakes without a multiple-attack requirement when breeding for passives.</summary>
    AttacksAndPassives,
}
