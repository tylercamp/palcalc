using PalCalc.Solver.PalReference.Properties;
using Serilog;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// Allocation-free aggregate diagnostics for one attack-solving run.
/// </summary>
internal sealed class AttackSolverDiagnostics
{
    private static readonly ILogger logger = Serilog.Log.ForContext<AttackSolverDiagnostics>();
    private const int PruneSampleRate = 256;
    // The rate is a power of two, allowing a thread-local bitmask check instead
    // of a modulo in this hot path. Exact aggregate counts are still recorded;
    // only the per-inheritance-mode breakdown is sampled to avoid three atomics.
    private const int PruneSampleMask = PruneSampleRate - 1;

    [ThreadStatic]
    private static int pruneSampleCounter;

    private long compositionCalls;
    private long parentEntryPairs;
    private long baselineAttempts;
    private long normalAttempts;
    private long cakeAttempts;
    private long cakeFirstPrunedAttempts;
    private long pruneSamples;
    private long sampledBaselinePrunedAttempts;
    private long sampledNormalPrunedAttempts;
    private long sampledCakePrunedAttempts;
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
    private CompositionSnapshot previousCompositionSnapshot;

    public void RecordComposition(
        long parentEntryPairCount,
        long baselineAttemptCount,
        long normalAttemptCount,
        long cakeAttemptCount,
        long baselinePrunedAttemptCount,
        long normalPrunedAttemptCount,
        long cakePrunedAttemptCount,
        int emittedEntryCount,
        int retainedEntryCount
    )
    {
        Interlocked.Increment(ref compositionCalls);
        Interlocked.Add(ref parentEntryPairs, parentEntryPairCount);
        Interlocked.Add(ref baselineAttempts, baselineAttemptCount);
        Interlocked.Add(ref normalAttempts, normalAttemptCount);
        Interlocked.Add(ref cakeAttempts, cakeAttemptCount);
        Interlocked.Add(
            ref cakeFirstPrunedAttempts,
            baselinePrunedAttemptCount + normalPrunedAttemptCount + cakePrunedAttemptCount
        );
        if ((++pruneSampleCounter & PruneSampleMask) == 0)
        {
            Interlocked.Increment(ref pruneSamples);
            Interlocked.Add(ref sampledBaselinePrunedAttempts, baselinePrunedAttemptCount);
            Interlocked.Add(ref sampledNormalPrunedAttempts, normalPrunedAttemptCount);
            Interlocked.Add(ref sampledCakePrunedAttempts, cakePrunedAttemptCount);
        }
        Interlocked.Add(ref emittedEntries, emittedEntryCount);
        Interlocked.Add(ref retainedEntries, retainedEntryCount);
        UpdateMax(
            ref maxParentEntryPairs,
            parentEntryPairCount > int.MaxValue ? int.MaxValue : (int)parentEntryPairCount
        );
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

    public void LogCompositionStep(int step)
    {
        var current = new CompositionSnapshot(
            Interlocked.Read(ref compositionCalls),
            Interlocked.Read(ref parentEntryPairs),
            Interlocked.Read(ref baselineAttempts),
            Interlocked.Read(ref normalAttempts),
            Interlocked.Read(ref cakeAttempts),
            Interlocked.Read(ref cakeFirstPrunedAttempts),
            Interlocked.Read(ref pruneSamples),
            Interlocked.Read(ref sampledBaselinePrunedAttempts),
            Interlocked.Read(ref sampledNormalPrunedAttempts),
            Interlocked.Read(ref sampledCakePrunedAttempts),
            Interlocked.Read(ref emittedEntries),
            Interlocked.Read(ref retainedEntries)
        );
        var delta = current - previousCompositionSnapshot;
        previousCompositionSnapshot = current;
        if (delta.Calls == 0)
            return;

        logger.Debug(
            "Attack composition step profile: step={Step}, calls={Calls}, parentEntryPairs={ParentEntryPairs}, attempts={BaselineAttempts}+{NormalAttempts}+{CakeAttempts}, cakeFirstPruned={CakeFirstPruned}, pruneSample=1/{SampleRate}:{PruneSamples}:{BaselinePruned}+{NormalPruned}+{CakePruned}, entries={EmittedEntries}->{RetainedEntries}",
            step,
            delta.Calls,
            delta.ParentEntryPairs,
            delta.BaselineAttempts,
            delta.NormalAttempts,
            delta.CakeAttempts,
            delta.CakeFirstPrunedAttempts,
            PruneSampleRate,
            delta.PruneSamples,
            delta.SampledBaselinePrunedAttempts,
            delta.SampledNormalPrunedAttempts,
            delta.SampledCakePrunedAttempts,
            delta.EmittedEntries,
            delta.RetainedEntries
        );
    }

    public void Log()
    {
        var calls = Interlocked.Read(ref compositionCalls);
        if (calls == 0)
            return;

        logger.Debug(
            "Attack composition profile: calls={Calls}, parentEntryPairs={ParentEntryPairs}, attempts={BaselineAttempts}+{NormalAttempts}+{CakeAttempts}, cakeFirstPruned={CakeFirstPruned}, pruneSample=1/{SampleRate}:{PruneSamples}:{BaselinePruned}+{NormalPruned}+{CakePruned}, entries={EmittedEntries}->{RetainedEntries}, maxParentEntryPairs={MaxParentEntryPairs}, maxEntries={MaxEmittedEntries}->{MaxRetainedEntries}",
            calls,
            Interlocked.Read(ref parentEntryPairs),
            Interlocked.Read(ref baselineAttempts),
            Interlocked.Read(ref normalAttempts),
            Interlocked.Read(ref cakeAttempts),
            Interlocked.Read(ref cakeFirstPrunedAttempts),
            PruneSampleRate,
            Interlocked.Read(ref pruneSamples),
            Interlocked.Read(ref sampledBaselinePrunedAttempts),
            Interlocked.Read(ref sampledNormalPrunedAttempts),
            Interlocked.Read(ref sampledCakePrunedAttempts),
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

    private readonly record struct CompositionSnapshot(
        long Calls,
        long ParentEntryPairs,
        long BaselineAttempts,
        long NormalAttempts,
        long CakeAttempts,
        long CakeFirstPrunedAttempts,
        long PruneSamples,
        long SampledBaselinePrunedAttempts,
        long SampledNormalPrunedAttempts,
        long SampledCakePrunedAttempts,
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
            left.BaselineAttempts - right.BaselineAttempts,
            left.NormalAttempts - right.NormalAttempts,
            left.CakeAttempts - right.CakeAttempts,
            left.CakeFirstPrunedAttempts - right.CakeFirstPrunedAttempts,
            left.PruneSamples - right.PruneSamples,
            left.SampledBaselinePrunedAttempts - right.SampledBaselinePrunedAttempts,
            left.SampledNormalPrunedAttempts - right.SampledNormalPrunedAttempts,
            left.SampledCakePrunedAttempts - right.SampledCakePrunedAttempts,
            left.EmittedEntries - right.EmittedEntries,
            left.RetainedEntries - right.RetainedEntries
        );
    }
}
