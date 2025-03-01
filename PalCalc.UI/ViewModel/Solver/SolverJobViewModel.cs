using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using QuickGraph.Algorithms;
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
        private Thread thread;
        private Stopwatch sw;

        // (dispatcher.HasShutdownStarted checks added in case a job fails, causes UI shutdown, and remaining
        // jobs continue due to Dispose but throw more errors due to UI env. shutdown)
        private Dispatcher dispatcher;
        private BreedingSolver solver;
        private CancellationTokenSource tokenSource;
        private SolverStateController solverController;

        public PalSpecifierViewModel Specifier { get; }

        private int lastStepIndex = -1;

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
            BreedingSolver solver,
            PalSpecifierViewModel spec,
            int saveStateId
        )
        {
            this.dispatcher = dispatcher;
            this.solver = solver;

            Specifier = spec;

            tokenSource = new CancellationTokenSource();
            solverController = new SolverStateController()
            {
                CancellationToken = tokenSource.Token
            };

            solver.SolverStateUpdated += Solver_SolverStateUpdated;

            SaveStateId = saveStateId;
            CurrentState = SolverState.Paused;
        }

        public void Run()
        {
            if (thread == null)
            {
                thread = new Thread(() => RunSolver(Specifier.ModelObject));

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

            // we'd prefer to get the current state from the solver's update events,
            // but not everything is wired up to emit those events (namely `WorkingSet`)
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

        private void RunSolver(PalSpecifier spec)
        {
            try
            {
                List<IPalReference> results;
                try
                {
                    results = solver.SolveFor(spec, solverController);
                }
                catch (OperationCanceledException)
                {
                    results = [];
                }

                if (dispatcher.HasShutdownStarted) return;

                // general simplification pass, get the best result for each potentially
                // interesting combination of result properties
                var resultsTable = new PalPropertyGrouping(PalProperty.Combine(
                    PalProperty.EffectivePassives,
                    PalProperty.NumBreedingSteps,
                    p => p.AllReferences().Select(r => r.Location.GetType()).Distinct().SetHash()
                ));
                resultsTable.AddRange(results);
                resultsTable.FilterAll(PruningRulesBuilder.Default, tokenSource.Token);

                // final simplification pass, ignore any results which are over 2x the effort of the fastest option
                resultsTable = resultsTable.BuildNew(PalProperty.Combine(
                    PalProperty.EffectivePassives
                ));
                resultsTable.FilterAll(g =>
                {
                    // (though if "the fastest option" is just a pal we already own with 0 effort, don't count that)
                    var nonZero = g.Where(r => r.BreedingEffort > TimeSpan.Zero).ToList();
                    if (nonZero.Count != 0)
                    {
                        var fastest = g.Where(r => r.BreedingEffort > TimeSpan.Zero).Min(r => r.BreedingEffort);
                        return g.Where(r => r.BreedingEffort <= fastest * 2);
                    }
                    else
                    {
                        return g.Take(1);
                    }
                });

                results = resultsTable.All.ToList();

                dispatcher.Invoke(() =>
                {
                    if (!tokenSource.IsCancellationRequested)
                    {
                        Results = results;
                        JobCompleted?.Invoke(this);
                    }
                    else
                    {
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

        private void Solver_SolverStateUpdated(SolverStatus obj)
        {
            if (dispatcher.HasShutdownStarted) return;

            string FormatNum(long num) => num.ToString("#,##");

            if (sw == null)
                sw = Stopwatch.StartNew();

            dispatcher.BeginInvoke(() =>
            {
                if (!obj.Canceled)
                    CurrentState = obj.Paused ? SolverState.Paused : SolverState.Running;

                var numTotalSteps = (double)(1 + obj.TargetSteps);
                int overallStep = 0;
                switch (obj.CurrentPhase)
                {
                    case SolverPhase.Initializing:
                        SolverStatusMessage = LocalizationCodes.LV_SOLVER_STATUS_INITIALIZING.Bind();
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

                    case SolverPhase.Finished:
                        if (obj.Canceled)
                        {
                            SolverStatusMessage = null;
                        }
                        else
                        {
                            SolverStatusMessage = LocalizationCodes.LC_SOLVER_STATUS_FINISHED.Bind(sw.Elapsed.TimeSpanSecondsStr());
                            overallStep = (int)numTotalSteps;
                            StepProgress = 100;
                            StepStatusMessage = LocalizationCodes.LC_SOLVER_STEP_STATUS_DONE.Bind(FormatNum(obj.TotalWorkProcessedCount));
                        }
                        break;
                }

                SolverProgress = 100 * overallStep / numTotalSteps;
            }, DispatcherPriority.Send);
        }
    }
}
