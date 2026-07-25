namespace PalCalc.Solver;

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
    public bool Canceled { get; init; }
    public bool Paused { get; init; }

    public long CurrentWorkSize { get; init; }
    public long WorkProcessedCount { get; init; }
    public long TotalWorkProcessedCount { get; init; }
}
