namespace PalCalc.Solver.PalReference.Properties;

/// <summary>
/// Orders attack outcomes by the solver's deliberately lossy objective:
/// estimated Special Cakes first, then production effort.
/// </summary>
internal sealed class AttackProfileEntryComparer : IComparer<AttackProfileEntry>
{
    public static AttackProfileEntryComparer Instance { get; } = new();

    public int Compare(AttackProfileEntry left, AttackProfileEntry right)
    {
        var comparison = CompareCosts(left, right);
        return comparison != 0
            ? comparison
            : left.LearnedTargetMask.CompareTo(right.LearnedTargetMask);
    }

    public static int CompareCosts(in AttackProfileEntry left, in AttackProfileEntry right)
    {
        var comparison = left.TotalSpecialCakes.CompareTo(right.TotalSpecialCakes);
        if (comparison != 0) return comparison;

        comparison = left.BreedingEffort.CompareTo(right.BreedingEffort);
        if (comparison != 0) return comparison;

        comparison = left.SelfBreedings.CompareTo(right.SelfBreedings);
        if (comparison != 0) return comparison;

        return left.SelfUsesSpecialCake.CompareTo(right.SelfUsesSpecialCake);
    }
}
