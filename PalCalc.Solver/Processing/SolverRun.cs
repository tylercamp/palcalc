using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing.Search;
using Serilog;

namespace PalCalc.Solver.Processing
{
    /// <summary>
    /// Coordinates one complete search from starting candidates through final
    /// surgery and output constraints.
    /// </summary>
    internal sealed class SolverRun(
        SolverRunContext context,
        Action<SolverStatus> stateUpdated,
        TimeSpan stateUpdateInterval
    )
    {
        private static readonly ILogger logger = Log.ForContext<SolverRun>();

        private BreedingSolverSettings settings => context.Settings;

        public List<IPalReference> Execute()
        {
            var spec = context.Target;
            var controller = context.Controller;

            if (spec.RequiredPassives.Count > GameConstants.MaxTotalPassives)
            {
                throw new Exception("Target passive skill count cannot exceed max number of passive skills for a single pal");
            }

            var statusMsg = new SolverStatus()
            {
                CurrentPhase = SolverPhase.Initializing,
                CurrentStepIndex = 0,
                TargetSteps = settings.MaxSolverIterations,
                IsCanceled = controller.CancellationToken.IsCancellationRequested,
                IsPaused = controller.IsPaused,
            };
            stateUpdated?.Invoke(statusMsg);

            var frontier = new SearchFrontier(
                spec,
                new InitialPalBuilder(
                    settings,
                    context.Mechanics,
                    context.BreedingDB
                ).Build(spec),
                settings.MaxThreads,
                controller,
                context.SelectionPolicy
            );
            var batchExecutor = new ParallelBatchExecutor(context, stateUpdateInterval);

            // Repeatedly breed newly useful parent pairs. Each pass only
            // schedules combinations introduced by the previous frontier change.

            for (int s = 0; s < settings.MaxSolverIterations; s++)
            {
                if (controller.CancellationToken.IsCancellationRequested) break;

                var expansionContext = new CandidateExpansionContext(
                    StepIndex: s,
                    Target: spec,
                    PreFilter: new CandidatePreFilter(
                        target: spec,
                        maxEffort: settings.MaxEffort,
                        selectionPolicy: context.SelectionPolicy,
                        frontier: frontier,
                        palIds: settings.DB.PalsById.Keys
                    )
                );

                var delta = frontier.ExpandPairs(work =>
                {
                    logger.Debug("Performing breeding step {step} with {numWork} work items", s+1, work.Count);

                    statusMsg = statusMsg with
                    {
                        CurrentPhase = SolverPhase.Breeding,
                        CurrentStepIndex = s,
                        IsCanceled = controller.CancellationToken.IsCancellationRequested,
                        IsPaused = controller.IsPaused,
                        CurrentWorkSize = work.Count,
                        WorkProcessedCount = 0,
                    };
                    stateUpdated?.Invoke(statusMsg);

                    var execution = batchExecutor.Execute(
                        work,
                        expansionContext,
                        progress =>
                        {
                            statusMsg = statusMsg with
                            {
                                WorkProcessedCount = progress.WorkProcessedCount,
                                IsPaused = progress.IsPaused,
                                IsCanceled = progress.IsCanceled,
                            };
                            stateUpdated?.Invoke(statusMsg);
                        }
                    );

                    statusMsg = statusMsg with
                    {
                        WorkProcessedCount = execution.WorkProcessedCount,
                        TotalWorkProcessedCount = statusMsg.TotalWorkProcessedCount + execution.WorkProcessedCount,
                        IsCanceled = controller.CancellationToken.IsCancellationRequested,
                        IsPaused = controller.IsPaused,
                    };
                    stateUpdated?.Invoke(statusMsg);

                    return execution.Candidates;
                });

                if (controller.CancellationToken.IsCancellationRequested) break;

                if (!delta.Changed)
                {
                    logger.Debug("Last pass found no new useful options, stopping iteration early");
                    break;
                }
            }

            statusMsg = statusMsg with
            {
                IsCanceled = controller.CancellationToken.IsCancellationRequested,
                IsPaused = controller.IsPaused,
                CurrentPhase = SolverPhase.Finished,
            };
            stateUpdated?.Invoke(statusMsg);

            var resultPostProcessor = new ResultPostProcessor(spec, settings, controller);
            resultPostProcessor.ApplySurgery(frontier);
            return resultPostProcessor.Finalize(frontier.TerminalResults);
        }
    }
}
