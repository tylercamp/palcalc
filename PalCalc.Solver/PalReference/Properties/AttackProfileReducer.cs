namespace PalCalc.Solver.PalReference.Properties;

internal static class AttackProfileReducer
{
    private const int MaxStackEntries = 256;

    /// <summary>
    /// Returns whether the capabilities in `required` are also covered by `provider`. i.e.,
    /// by satisfying `provider`, we also satisfy `required`.
    /// </summary>
    public static bool Covers(in AttackProfileEntry provider, in AttackProfileEntry required) =>
        // (The use of `in` on the param acts like `ref`, avoiding a copy, but prevents the need
        // for `ref` by the caller and prevents the param value from being overwritten.)
        (provider.LearnedTargetMask & required.LearnedTargetMask) == required.LearnedTargetMask &&
        provider.TotalSpecialCakes <= required.TotalSpecialCakes &&
        provider.SelfBreedings <= required.SelfBreedings &&
        (!provider.SelfUsesSpecialCake || required.SelfUsesSpecialCake) &&
        provider.BreedingEffort <= required.BreedingEffort;

    public static AttackProfile Reduce(ReadOnlySpan<AttackProfileEntry> entries) =>
        Reduce(hasNoopAttack: false, entries);

    /// <summary>
    /// Returns a combined `AttackProfile` containing a minimal representation of possible
    /// attack profiles described by `entries`.
    /// </summary>
    public static AttackProfile Reduce(bool hasNoopAttack, ReadOnlySpan<AttackProfileEntry> entries)
    {
        // `IsCovered` is an expensive call, do one pass up-front and track the results, then copy the un-covered items
        Span<bool> covered = entries.Length <= MaxStackEntries
            ? stackalloc bool[entries.Length]
            : new bool[entries.Length];
        var retainedCount = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            covered[i] = IsCovered(entries, i);
            if (!covered[i])
                retainedCount++;
        }

        var retained = new AttackProfileEntry[retainedCount];
        for (int i = 0, destination = 0; i < entries.Length; i++)
            if (!covered[i])
                retained[destination++] = entries[i];

        return new AttackProfile(hasNoopAttack, retained);
    }

    /// <summary>
    /// Checks the list of attack profiles, and returns whether the profile at `requiredIndex`
    /// is already covered by some other profile in the list.
    /// </summary>
    private static bool IsCovered(ReadOnlySpan<AttackProfileEntry> entries, int requiredIndex)
    {
        for (var providerIndex = 0; providerIndex < entries.Length; providerIndex++)
        {
            if (providerIndex == requiredIndex || !Covers(entries[providerIndex], entries[requiredIndex]))
                continue;

            if (entries[providerIndex] != entries[requiredIndex] || providerIndex < requiredIndex)
                return true;
        }

        return false;
    }
}
