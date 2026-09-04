using Serilog;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>Compact aggregate diagnostics for attack search and reconstruction.</summary>
internal sealed class AttackSolverDiagnostics
{
    private static readonly ILogger logger = Serilog.Log.ForContext<AttackSolverDiagnostics>();

    private long compositionCalls;
    private long parentEntryPairs;
    private long emittedEntries;
    private long retainedEntries;
    private long maxInputProfileSize;
    private long maxOutputProfileSize;
    private long terminalCandidatesShortlisted;
    private long materializationAttempts;
    private long materializationSuccesses;
    private long materializationConstraintRejections;
    private CompositionSnapshot previousSnapshot;

    public void RecordComposition(
        long parentEntryPairCount,
        long emittedEntryCount,
        long retainedEntryCount,
        int inputProfileSize
    )
    {
        Interlocked.Increment(ref compositionCalls);
        Interlocked.Add(ref parentEntryPairs, parentEntryPairCount);
        Interlocked.Add(ref emittedEntries, emittedEntryCount);
        Interlocked.Add(ref retainedEntries, retainedEntryCount);
        UpdateMaximum(ref maxInputProfileSize, inputProfileSize);
        UpdateMaximum(ref maxOutputProfileSize, retainedEntryCount);
    }

    public void RecordTerminalShortlist(int count) =>
        Interlocked.Add(ref terminalCandidatesShortlisted, count);

    public void RecordMaterializationAttempt() =>
        Interlocked.Increment(ref materializationAttempts);

    public void RecordMaterializationSuccess() =>
        Interlocked.Increment(ref materializationSuccesses);

    public void RecordMaterializationConstraintRejection() =>
        Interlocked.Increment(ref materializationConstraintRejections);

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
        if (snapshot.Calls != 0 || snapshot.MaterializationAttempts != 0)
            Log("Attack solver profile", null, snapshot);
    }

    private CompositionSnapshot Snapshot() => new(
        Interlocked.Read(ref compositionCalls),
        Interlocked.Read(ref parentEntryPairs),
        Interlocked.Read(ref emittedEntries),
        Interlocked.Read(ref retainedEntries),
        Interlocked.Read(ref maxInputProfileSize),
        Interlocked.Read(ref maxOutputProfileSize),
        Interlocked.Read(ref terminalCandidatesShortlisted),
        Interlocked.Read(ref materializationAttempts),
        Interlocked.Read(ref materializationSuccesses),
        Interlocked.Read(ref materializationConstraintRejections)
    );

    private static void Log(string message, int? step, CompositionSnapshot snapshot)
    {
        logger.Debug(
            "{Message}: step={Step}, calls={Calls}, parentEntryPairs={ParentEntryPairs}, entries={EmittedEntries}->{RetainedEntries}, profileSize={MaxInputProfileSize}->{MaxOutputProfileSize}, shortlist={TerminalCandidatesShortlisted}, materialization={MaterializationAttempts}->{MaterializationSuccesses}, constraintRejections={MaterializationConstraintRejections}",
            message,
            step,
            snapshot.Calls,
            snapshot.ParentEntryPairs,
            snapshot.EmittedEntries,
            snapshot.RetainedEntries,
            snapshot.MaxInputProfileSize,
            snapshot.MaxOutputProfileSize,
            snapshot.TerminalCandidatesShortlisted,
            snapshot.MaterializationAttempts,
            snapshot.MaterializationSuccesses,
            snapshot.MaterializationConstraintRejections
        );
    }

    private static void UpdateMaximum(ref long location, long value)
    {
        while (true)
        {
            var current = Interlocked.Read(ref location);
            if (value <= current ||
                Interlocked.CompareExchange(ref location, value, current) == current)
                return;
        }
    }

    private readonly record struct CompositionSnapshot(
        long Calls,
        long ParentEntryPairs,
        long EmittedEntries,
        long RetainedEntries,
        long MaxInputProfileSize,
        long MaxOutputProfileSize,
        long TerminalCandidatesShortlisted,
        long MaterializationAttempts,
        long MaterializationSuccesses,
        long MaterializationConstraintRejections
    )
    {
        public static CompositionSnapshot operator -(
            CompositionSnapshot left,
            CompositionSnapshot right
        ) => new(
            left.Calls - right.Calls,
            left.ParentEntryPairs - right.ParentEntryPairs,
            left.EmittedEntries - right.EmittedEntries,
            left.RetainedEntries - right.RetainedEntries,
            left.MaxInputProfileSize,
            left.MaxOutputProfileSize,
            left.TerminalCandidatesShortlisted - right.TerminalCandidatesShortlisted,
            left.MaterializationAttempts - right.MaterializationAttempts,
            left.MaterializationSuccesses - right.MaterializationSuccesses,
            left.MaterializationConstraintRejections - right.MaterializationConstraintRejections
        );
    }
}
