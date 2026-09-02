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
        Span<int> retainedIndexes = entries.Length <= MaxStackEntries
            ? stackalloc int[entries.Length]
            : new int[entries.Length];
        var retainedCount = 0;
        for (var candidateIndex = 0; candidateIndex < entries.Length; candidateIndex++)
        {
            var isCovered = false;
            for (var i = 0; i < retainedCount; i++)
            {
                if (!Covers(entries[retainedIndexes[i]], entries[candidateIndex]))
                    continue;

                isCovered = true;
                break;
            }

            if (isCovered)
                continue;

            var destination = 0;
            for (var i = 0; i < retainedCount; i++)
            {
                var retainedIndex = retainedIndexes[i];
                if (!Covers(entries[candidateIndex], entries[retainedIndex]))
                    retainedIndexes[destination++] = retainedIndex;
            }

            retainedIndexes[destination] = candidateIndex;
            retainedCount = destination + 1;
        }

        var retained = new AttackProfileEntry[retainedCount];
        for (var i = 0; i < retainedCount; i++)
            retained[i] = entries[retainedIndexes[i]];

        return new AttackProfile(hasNoopAttack, retained);
    }
}
