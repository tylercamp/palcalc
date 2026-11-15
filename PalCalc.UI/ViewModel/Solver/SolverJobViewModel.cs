using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing;
using PalCalc.Solver.ResultPruning;
using PalCalc.Solver.Utils;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using QuickGraph.Algorithms;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Windows.Devices.Geolocation;

namespace PalCalc.UI.ViewModel.Solver
{
    public class SolverJobViewModel : ObservableObject, IDisposable
    {
        private static ILogger logger = Log.ForContext<SolverJobViewModel>();

        private Thread thread;
        private Stopwatch sw;

        // (dispatcher.HasShutdownStarted checks added in case a job fails, causes UI shutdown, and remaining
        // jobs continue due to Dispose but throw more errors due to UI env. shutdown)
        private Dispatcher dispatcher;
        private BreedingSolver solver = new();
        private BreedingSolverRequest solverRequest;
        private CancellationTokenSource tokenSource;
        private SolverStateController solverController;

        public PalSpecifierViewModel Specifier { get; }

        private int lastStepIndex = -1;
        private long totalWorkProcessedCount;

        protected override void OnPropertyChanging(PropertyChangingEventArgs e)
        {
            if (dispatcher.HasShutdownStarted) return;

            if (Thread.CurrentThread == dispatcher.Thread)
                base.OnPropertyChanging(e);
            else
                dispatcher.BeginInvoke(() => base.OnPropertyChanging(e));
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (dispatcher.HasShutdownStarted) return;

            if (Thread.CurrentThread == dispatcher.Thread)
                base.OnPropertyChanged(e);
            else
                dispatcher.BeginInvoke(() => base.OnPropertyChanged(e));
        }

        private SolverState currentState;
        public SolverState CurrentState
        {
            get => currentState;
            private set
            {
                if (SetProperty(ref currentState, value))
                {
                    OnPropertyChanged(nameof(IsActive));
                    OnPropertyChanged(nameof(IsInactive));
                }
            }
        }

        private double solverProgress;
        public double SolverProgress
        {
            get => solverProgress;
            private set => SetProperty(ref solverProgress, value);
        }

        private double stepProgress;
        public double StepProgress
        {
            get => stepProgress;
            private set => SetProperty(ref stepProgress, value);
        }

        private ILocalizedText solverStatusMessage;
        public ILocalizedText SolverStatusMessage
        {
            get => solverStatusMessage;
            private set => SetProperty(ref solverStatusMessage, value);
        }

        private ILocalizedText stepStatusMessage;
        public ILocalizedText StepStatusMessage
        {
            get => stepStatusMessage;
            private set => SetProperty(ref stepStatusMessage, value);
        }

        public bool IsActive => CurrentState != SolverState.Idle;
        public bool IsInactive => !IsActive;

        // state ID associated with the save when this job was created, used to determine whether
        // the results need to be refreshed once the solver completes
        public int SaveStateId { get; }

        public List<IPalReference> Results { get; private set; }

        public event Action<SolverJobViewModel> JobStopped;
        public event Action<SolverJobViewModel> JobCompleted;
        public event Action<SolverJobViewModel> JobCancelled;

        public SolverJobViewModel(
            Dispatcher dispatcher,
            BreedingSolverRequest solverRequest,
            PalSpecifierViewModel spec,
            int saveStateId
        )
        {
            this.dispatcher = dispatcher;
            this.solverRequest = solverRequest;

            Specifier = spec;

            tokenSource = new CancellationTokenSource();
            solverController = new SolverStateController(
                tokenSource.Token
            );

            solver.StatusUpdated += Solver_StatusUpdated;

            SaveStateId = saveStateId;
            CurrentState = SolverState.Paused;
        }

        public void Run()
        {
            if (thread == null)
            {
                thread = new Thread(RunSolver);

                thread.Priority = ThreadPriority.BelowNormal;
                thread.Start();
            }

            solverController.Resume();
            CurrentState = SolverState.Running;
        }

        public void Pause()
        {
            if (thread == null) return;

            solverController.Pause();

            // Reflect the requested state immediately rather than waiting for
            // the next periodic solver status update.
            CurrentState = SolverState.Paused;
        }

        public void Cancel()
        {
            if (thread == null)
            {
                JobCancelled?.Invoke(this);
                JobStopped?.Invoke(this);
                CurrentState = SolverState.Idle;
            }
            else
            {
                tokenSource?.Cancel();
                solverController?.Resume();
            }
        }

        public void Dispose()
        {
            Cancel();
            thread?.Join();
            tokenSource.Dispose();
        }

        private void RunSolver()
        {
            try
            {
                IReadOnlyList<IPalReference> results;
                try
                {
                    results = solver
                        .Solve(solverRequest, solverController)
                        .Results;
                }
                catch (OperationCanceledException)
                {
                    results = [];
                }

                if (dispatcher.HasShutdownStarted) return;

                var simplified = SimplifyResults(results, tokenSource.Token);

                dispatcher.Invoke(() =>
                {
                    if (!tokenSource.IsCancellationRequested)
                    {
                        var publishTimer = Stopwatch.StartNew();
                        Results = simplified;
                        JobCompleted?.Invoke(this);
                        logger.Debug("Published {ResultCount} results in {ElapsedMs} ms", simplified.Count, publishTimer.ElapsedMilliseconds);

                        SolverStatusMessage = LocalizationCodes.LC_SOLVER_STATUS_FINISHED.Bind(sw.Elapsed.TimeSpanSecondsStr());
                        StepStatusMessage = LocalizationCodes.LC_SOLVER_STEP_STATUS_DONE.Bind(totalWorkProcessedCount.ToString("#,##"));
                    }
                    else
                    {
                        SolverStatusMessage = null;
                        JobCancelled?.Invoke(this);
                    }

                    JobStopped?.Invoke(this);

                    CurrentState = SolverState.Idle;
                });
            }
            catch (Exception e)
            {
                dispatcher.BeginInvoke(() =>
                {
                    // re-throw on UI thread so the app crashes (instead of hangs) with proper error handling
                    throw new Exception("Unhandled error during solver operation", e);
                });
            }
        }

        internal static List<IPalReference> SimplifyResults(IEnumerable<IPalReference> results, CancellationToken token)
        {
            var timer = Stopwatch.StartNew();
            int? inputCount = results.TryGetNonEnumeratedCount(out var count) ? count : null;

            // general simplification pass, get the best result for each potentially
            // interesting combination of result properties
            var resultsTable = new PalResultGrouping(PalResultProperty.Combine(
                PalResultProperty.EffectivePassives,
                PalResultProperty.NumBreedingSteps,
                PalResultProperty.SpecialCakes,
                PalResultProperty.CreateAncestryLocationTypeGrouping()
            ));

            resultsTable.AddRange(results);
            resultsTable.FilterAll(ResultPruningPolicy.Default, token);

            // Limit cakes relative to the fastest path with the same final passives
            resultsTable = resultsTable.BuildNew(PalResultProperty.Combine(
                PalResultProperty.EffectivePassives
            ));
            resultsTable.FilterAll(g =>
            {
                var fastest = g.Min(r => r.BreedingEffort);
                // Use the cheapest tied fastest path so input order doesn't change the cutoff.
                var maxCakes = 2L * g.Where(r => r.BreedingEffort == fastest)
                    .Min(r => r.NumTotalEggs);
                return g.Where(r => r.NumTotalEggs <= maxCakes);
            });

            // final simplification pass, within each cake tier, ignore results over 2x its fastest option
            resultsTable = resultsTable.BuildNew(PalResultProperty.Combine(
                PalResultProperty.EffectivePassives,
                PalResultProperty.SpecialCakes
            ));
            resultsTable.FilterAll(g =>
            {
                // A pal we already own shouldn't set the effort cutoff for paths that require breeding.
                var fastest = g.Where(r => r.BreedingEffort > TimeSpan.Zero).Min(r => (TimeSpan?)r.BreedingEffort);
                return fastest is { } effort ? g.Where(r => r.BreedingEffort <= effort * 2) : g.Take(1);
            });

            var res = resultsTable.All.ToList();
            logger.Debug("Final simplification retained {ResultCount} of {InputCount} candidates in {ElapsedMs} ms (canceled: {Canceled})",
                res.Count, inputCount, timer.ElapsedMilliseconds, token.IsCancellationRequested);
            return res;
        }

        private void Solver_StatusUpdated(SolverStatus obj)
        {
            if (dispatcher.HasShutdownStarted) return;

            // The solver finishes before we've simplified and published the display results.
            if (obj.CurrentPhase == SolverPhase.Finished)
            {
                totalWorkProcessedCount = obj.TotalWorkProcessedCount;
                return;
            }

            string FormatNum(long num) => num.ToString("#,##");

            if (sw == null)
                sw = Stopwatch.StartNew();

            dispatcher.BeginInvoke(() =>
            {
                if (!obj.IsCanceled)
                    CurrentState = obj.IsPaused
                        ? SolverState.Paused
                        : SolverState.Running;

                var numTotalSteps = (double)(1 + obj.TargetSteps);
                int overallStep = 0;
                switch (obj.CurrentPhase)
                {
                    case SolverPhase.Initializing:
                        SolverStatusMessage = LocalizationCodes.LC_SOLVER_STATUS_INITIALIZING.Bind();
                        overallStep = 0;
                        lastStepIndex = -1;

                        StepProgress = 0;
                        StepStatusMessage = null;
                        break;

                    case SolverPhase.Breeding:
                        SolverStatusMessage = LocalizationCodes.LC_SOLVER_STATUS_BREEDING.Bind(
                            new
                            {
                                StepNum = obj.CurrentStepIndex + 1,
                                WorkSize = FormatNum(obj.CurrentWorkSize),
                            }
                        );
                        overallStep = 1 + obj.CurrentStepIndex;

                        StepProgress = 100 * (obj.WorkProcessedCount / (double)obj.CurrentWorkSize);
                        StepStatusMessage = LocalizationCodes.LC_SOLVER_STEP_STATUS_BREEDING.Bind(
                            new { NumProcessed = FormatNum(obj.WorkProcessedCount), WorkSize = FormatNum(obj.CurrentWorkSize) }
                        );

                        if (obj.CurrentStepIndex != lastStepIndex)
                        {
                            lastStepIndex = obj.CurrentStepIndex;
                        }
                        break;

                    case SolverPhase.Finalizing:
                        SolverStatusMessage = LocalizationCodes.LC_SOLVER_STATUS_FINALIZING.Bind();
                        overallStep = (int)numTotalSteps;
                        StepProgress = 100;
                        StepStatusMessage = null;
                        break;
                }

                SolverProgress = 100 * overallStep / numTotalSteps;
            }, DispatcherPriority.Send);
        }
    }
}
