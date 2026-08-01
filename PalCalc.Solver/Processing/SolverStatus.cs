namespace PalCalc.Solver.Processing;

public enum SolverPhase
{
    Initializing,
    Breeding,
    Finished,
}

public sealed record SolverStatus
{
    public SolverPhase CurrentPhase { get; init; }
    public int CurrentStepIndex { get; init; }
    public int TargetSteps { get; init; }
    public bool IsCanceled { get; init; }
    public bool IsPaused { get; init; }

    public long CurrentWorkSize { get; init; }
    public long WorkProcessedCount { get; init; }
    public long TotalWorkProcessedCount { get; init; }
}
