namespace PalCalc.Solver.PalReference.Properties;

internal static class AttackProfileReducer
{
    /// <summary>
    /// Returns whether the capabilities in `required` are also covered by `provider`. i.e.,
    /// by satisfying `provider`, we also satisfy `required`.
    /// </summary>
    public static bool Covers(in AttackProfileEntry provider, in AttackProfileEntry required) =>
        (provider.LearnedTargetMask & required.LearnedTargetMask) == required.LearnedTargetMask &&
        provider.TotalSpecialCakes <= required.TotalSpecialCakes &&
        provider.BreedingEffort <= required.BreedingEffort &&
        provider.SelfBreedings <= required.SelfBreedings &&
        (!provider.SelfUsesSpecialCake || required.SelfUsesSpecialCake);

    public static AttackProfile Reduce(ReadOnlySpan<AttackProfileEntry> entries) =>
        Reduce(hasNoopAttack: false, entries);

    /// <summary>
    /// Returns a combined `AttackProfile` containing a minimal representation of possible
    /// attack profiles described by `entries`.
    /// </summary>
    public static AttackProfile Reduce(bool hasNoopAttack, ReadOnlySpan<AttackProfileEntry> entries)
    {
        var retainedCount = 0;
        for (var i = 0; i < entries.Length; i++)
            if (!IsCovered(entries, i))
                retainedCount++;

        var retained = new AttackProfileEntry[retainedCount];
        for (int i = 0, destination = 0; i < entries.Length; i++)
            if (!IsCovered(entries, i))
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
