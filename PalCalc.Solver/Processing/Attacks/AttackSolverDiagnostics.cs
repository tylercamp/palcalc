using PalCalc.Solver.PalReference.Properties;
using Serilog;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// Allocation-free aggregate diagnostics for one attack-solving run.
/// </summary>
internal sealed class AttackSolverDiagnostics
{
    private static readonly ILogger logger = Serilog.Log.ForContext<AttackSolverDiagnostics>();

    private long compositionCalls;
    private long parentEntryPairs;
    private long baselineAttempts;
    private long normalAttempts;
    private long cakeAttempts;
    private long cakeFirstPrunedAttempts;
    private long emittedEntries;
    private long retainedEntries;
    private int maxParentEntryPairs;
    private int maxEmittedEntries;
    private int maxRetainedEntries;

    private long reductionCalls;
    private long reductionInputEntries;
    private long reductionOutputEntries;
    private long reductionDistinctMasks;
    private int maxReductionInputEntries;
    private int maxReductionDistinctMasks;
    private int maxReductionOutputEntries;

    public void RecordComposition(
        int parent1Entries,
        int parent2Entries,
        long baselineAttemptCount,
        long normalAttemptCount,
        long cakeAttemptCount,
        long cakeFirstPrunedAttemptCount,
        int emittedEntryCount,
        int retainedEntryCount
    )
    {
        var pairCount = (long)parent1Entries * parent2Entries;
        Interlocked.Increment(ref compositionCalls);
        Interlocked.Add(ref parentEntryPairs, pairCount);
        Interlocked.Add(ref baselineAttempts, baselineAttemptCount);
        Interlocked.Add(ref normalAttempts, normalAttemptCount);
        Interlocked.Add(ref cakeAttempts, cakeAttemptCount);
        Interlocked.Add(ref cakeFirstPrunedAttempts, cakeFirstPrunedAttemptCount);
        Interlocked.Add(ref emittedEntries, emittedEntryCount);
        Interlocked.Add(ref retainedEntries, retainedEntryCount);
        UpdateMax(ref maxParentEntryPairs, pairCount > int.MaxValue ? int.MaxValue : (int)pairCount);
        UpdateMax(ref maxEmittedEntries, emittedEntryCount);
        UpdateMax(ref maxRetainedEntries, retainedEntryCount);
    }

    public void RecordReduction(
        int inputCount,
        int outputCount,
        int distinctMaskCount
    )
    {
        Interlocked.Increment(ref reductionCalls);
        Interlocked.Add(ref reductionInputEntries, inputCount);
        Interlocked.Add(ref reductionOutputEntries, outputCount);
        Interlocked.Add(ref reductionDistinctMasks, distinctMaskCount);
        UpdateMax(ref maxReductionInputEntries, inputCount);
        UpdateMax(ref maxReductionDistinctMasks, distinctMaskCount);
        UpdateMax(ref maxReductionOutputEntries, outputCount);
    }

    public void Log()
    {
        var calls = Interlocked.Read(ref compositionCalls);
        if (calls == 0)
            return;

        logger.Debug(
            "Attack composition profile: calls={Calls}, parentEntryPairs={ParentEntryPairs}, attempts={BaselineAttempts}+{NormalAttempts}+{CakeAttempts}, cakeFirstPruned={CakeFirstPrunedAttempts}, entries={EmittedEntries}->{RetainedEntries}, maxParentEntryPairs={MaxParentEntryPairs}, maxEntries={MaxEmittedEntries}->{MaxRetainedEntries}",
            calls,
            Interlocked.Read(ref parentEntryPairs),
            Interlocked.Read(ref baselineAttempts),
            Interlocked.Read(ref normalAttempts),
            Interlocked.Read(ref cakeAttempts),
            Interlocked.Read(ref cakeFirstPrunedAttempts),
            Interlocked.Read(ref emittedEntries),
            Interlocked.Read(ref retainedEntries),
            Volatile.Read(ref maxParentEntryPairs),
            Volatile.Read(ref maxEmittedEntries),
            Volatile.Read(ref maxRetainedEntries)
        );

        logger.Debug(
            "Attack reduction profile: calls={Calls}, entries={InputEntries}->{DistinctMasks}->{OutputEntries}, maxEntries={MaxInputEntries}->{MaxDistinctMasks}->{MaxOutputEntries}, entrySize={EntrySize}",
            Interlocked.Read(ref reductionCalls),
            Interlocked.Read(ref reductionInputEntries),
            Interlocked.Read(ref reductionDistinctMasks),
            Interlocked.Read(ref reductionOutputEntries),
            Volatile.Read(ref maxReductionInputEntries),
            Volatile.Read(ref maxReductionDistinctMasks),
            Volatile.Read(ref maxReductionOutputEntries),
            System.Runtime.CompilerServices.Unsafe.SizeOf<AttackProfileEntry>()
        );
    }

    private static void UpdateMax(ref int maximum, int value)
    {
        var current = Volatile.Read(ref maximum);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref maximum, value, current);
            if (previous == current)
                return;
            current = previous;
        }
    }
}
