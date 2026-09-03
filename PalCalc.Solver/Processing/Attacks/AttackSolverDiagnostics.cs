using Serilog;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// Compact aggregate diagnostics retained while the MaxEffort fallback is being characterized.
/// </summary>
internal sealed class AttackSolverDiagnostics
{
    private static readonly ILogger logger = Serilog.Log.ForContext<AttackSolverDiagnostics>();

    private long compositionCalls;
    private long parentEntryPairs;
    private long maxEffortFallbacks;
    private long maxEffortFallbackSuccesses;
    private long maxEffortFallbackPairs;
    private long emittedEntries;
    private long retainedEntries;
    private CompositionSnapshot previousSnapshot;

    public void RecordComposition(
        long parentEntryPairCount,
        long maxEffortFallbackCount,
        long maxEffortFallbackSuccessCount,
        long maxEffortFallbackPairCount,
        int emittedEntryCount,
        int retainedEntryCount
    )
    {
        Interlocked.Increment(ref compositionCalls);
        Interlocked.Add(ref parentEntryPairs, parentEntryPairCount);
        Interlocked.Add(ref maxEffortFallbacks, maxEffortFallbackCount);
        Interlocked.Add(ref maxEffortFallbackSuccesses, maxEffortFallbackSuccessCount);
        Interlocked.Add(ref maxEffortFallbackPairs, maxEffortFallbackPairCount);
        Interlocked.Add(ref emittedEntries, emittedEntryCount);
        Interlocked.Add(ref retainedEntries, retainedEntryCount);
    }

    public void LogCompositionStep(int step)
    {
        var current = Snapshot();
        var delta = current - previousSnapshot;
        previousSnapshot = current;
        if (delta.Calls != 0)
            Log("Attack composition step profile", step, delta);
    }

    public void Log()
    {
        var snapshot = Snapshot();
        if (snapshot.Calls != 0)
            Log("Attack composition profile", null, snapshot);
    }

    private CompositionSnapshot Snapshot() => new(
        Interlocked.Read(ref compositionCalls),
        Interlocked.Read(ref parentEntryPairs),
        Interlocked.Read(ref maxEffortFallbacks),
        Interlocked.Read(ref maxEffortFallbackSuccesses),
        Interlocked.Read(ref maxEffortFallbackPairs),
        Interlocked.Read(ref emittedEntries),
        Interlocked.Read(ref retainedEntries)
    );

    private static void Log(string message, int? step, CompositionSnapshot snapshot)
    {
        logger.Debug(
            "{Message}: step={Step}, calls={Calls}, parentEntryPairs={ParentEntryPairs}, maxEffortFallback={Fallbacks}->{FallbackSuccesses}:{FallbackPairs}, entries={EmittedEntries}->{RetainedEntries}",
            message,
            step,
            snapshot.Calls,
            snapshot.ParentEntryPairs,
            snapshot.MaxEffortFallbacks,
            snapshot.MaxEffortFallbackSuccesses,
            snapshot.MaxEffortFallbackPairs,
            snapshot.EmittedEntries,
            snapshot.RetainedEntries
        );
    }

    private readonly record struct CompositionSnapshot(
        long Calls,
        long ParentEntryPairs,
        long MaxEffortFallbacks,
        long MaxEffortFallbackSuccesses,
        long MaxEffortFallbackPairs,
        long EmittedEntries,
        long RetainedEntries
    )
    {
        public static CompositionSnapshot operator -(
            CompositionSnapshot left,
            CompositionSnapshot right
        ) => new(
            left.Calls - right.Calls,
            left.ParentEntryPairs - right.ParentEntryPairs,
            left.MaxEffortFallbacks - right.MaxEffortFallbacks,
            left.MaxEffortFallbackSuccesses - right.MaxEffortFallbackSuccesses,
            left.MaxEffortFallbackPairs - right.MaxEffortFallbackPairs,
            left.EmittedEntries - right.EmittedEntries,
            left.RetainedEntries - right.RetainedEntries
        );
    }
}
