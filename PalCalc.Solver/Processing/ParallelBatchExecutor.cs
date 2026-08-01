using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.Utils;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace PalCalc.Solver.Processing;

internal readonly record struct ParallelBatchProgress(
    long WorkProcessedCount,
    bool IsPaused,
    bool IsCanceled
);

internal readonly record struct ParallelBatchExecutionResult(
    List<IPalReference> Candidates,
    long WorkProcessedCount
);

/// <summary>
/// Divides pending parent pairs among worker threads, combines their candidate
/// lists, and reports aggregate progress or worker failures to the solver run.
/// </summary>
internal sealed class ParallelBatchExecutor(
    SolverRunContext context,
    TimeSpan progressUpdateInterval
)
{
    public ParallelBatchExecutionResult Execute(
        ILazyCartesianProduct<IPalReference> work,
        CandidateExpansionContext expansionContext,
        Action<ParallelBatchProgress> progressUpdated
    )
    {
        var controller = context.Controller;
        var settings = context.Settings;
        var progressEntries = new List<WorkBatchProgress>();
        var results = new ConcurrentBag<List<IPalReference>>();
        var chunksEnumerator = work
            .Chunks(work.Count.PreferredParallelBatchSize())
            .TakeUntilCancelled(controller.CancellationToken)
            .GetEnumerator();

        ExceptionDispatchInfo workerFailure = null;

        long ProcessedCount()
        {
            lock (progressEntries)
                return progressEntries.Sum(entry => entry.NumProcessed);
        }

        var progressLock = new object();
        ParallelBatchProgress? lastProgress = null;
        void EmitProgress()
        {
            lock (progressLock)
            {
                var progress = new ParallelBatchProgress(
                    WorkProcessedCount: ProcessedCount(),
                    IsPaused: controller.IsPaused,
                    IsCanceled: controller.CancellationToken.IsCancellationRequested
                );

                if (progress == lastProgress)
                    return;

                lastProgress = progress;
                progressUpdated(progress);
            }
        }

        void CaptureFailure(Exception exception) =>
            Interlocked.CompareExchange(
                ref workerFailure,
                ExceptionDispatchInfo.Capture(exception),
                null
            );

        void RunWorker()
        {
            try
            {
                // Pools remain worker-local. Neither the factory nor the expander
                // is shared with another worker.
                var expander = new CandidateExpander(
                    controller,
                    settings,
                    new ObjectPoolFactory(),
                    context.Mechanics,
                    context.BreedingDB
                );

                while (Volatile.Read(ref workerFailure) == null)
                {
                    IEnumerable<(IPalReference, IPalReference)> batch;
                    lock (chunksEnumerator)
                    {
                        if (
                            Volatile.Read(ref workerFailure) != null ||
                            !chunksEnumerator.MoveNext()
                        )
                        {
                            break;
                        }

                        batch = chunksEnumerator.Current;
                    }

                    var progress = new WorkBatchProgress();
                    lock (progressEntries)
                        progressEntries.Add(progress);

                    results.Add(
                        expander
                            .ExpandBatch(batch, progress, expansionContext)
                            .ToList()
                    );
                }
            }
            catch (Exception exception)
            {
                CaptureFailure(exception);
            }
        }

        void EmitProgressSafely(object _)
        {
            try
            {
                EmitProgress();
            }
            catch (Exception exception)
            {
                CaptureFailure(exception);
            }
        }

        var workThreads = Enumerable
            .Range(0, settings.MaxThreads)
            .Select(_ => new Thread(RunWorker)
            {
                Priority = ThreadPriority.BelowNormal,
            })
            .ToList();
        var startedThreads = new List<Thread>(workThreads.Count);
        var progressTimer = new Timer(
            EmitProgressSafely,
            null,
            (int)progressUpdateInterval.TotalMilliseconds,
            (int)progressUpdateInterval.TotalMilliseconds
        );

        try
        {
            foreach (var thread in workThreads)
            {
                thread.Start();
                startedThreads.Add(thread);
            }

            foreach (var thread in startedThreads)
                thread.Join();
        }
        catch (Exception exception)
        {
            CaptureFailure(exception);
        }
        finally
        {
            foreach (var thread in startedThreads.Where(thread => thread.IsAlive))
                thread.Join();

            progressTimer
                .DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
            chunksEnumerator.Dispose();
        }

        workerFailure?.Throw();

        return new(
            Candidates: results.SelectMany(batch => batch).ToList(),
            WorkProcessedCount: ProcessedCount()
        );
    }
}
