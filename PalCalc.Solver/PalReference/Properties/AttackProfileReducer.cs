namespace PalCalc.Solver.PalReference.Properties;

internal static class AttackProfileReducer
{
    public static bool Covers(in AttackProfileEntry provider, in AttackProfileEntry required) =>
        (provider.MasteredTargetMask & required.MasteredTargetMask) == required.MasteredTargetMask &&
        provider.TotalSpecialCakes <= required.TotalSpecialCakes &&
        provider.BreedingEffort <= required.BreedingEffort &&
        provider.SelfBreedings <= required.SelfBreedings &&
        (!provider.SelfUsesSpecialCake || required.SelfUsesSpecialCake);

    public static AttackProfile Reduce(ReadOnlySpan<AttackProfileEntry> entries)
    {
        var retainedCount = 0;
        for (var i = 0; i < entries.Length; i++)
            if (!IsCovered(entries, i))
                retainedCount++;

        var retained = new AttackProfileEntry[retainedCount];
        for (int i = 0, destination = 0; i < entries.Length; i++)
            if (!IsCovered(entries, i))
                retained[destination++] = entries[i];

        return new AttackProfile(retained);
    }

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
