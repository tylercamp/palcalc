using PalCalc.Solver.Processing;

namespace PalCalc.Solver;

/// <summary>
/// Runs a full breeding-path search and reports progress through status
/// snapshots. The request supplies all run-specific data and constraints.
/// </summary>
public sealed class BreedingSolver
{
    public event Action<SolverStatus> StatusUpdated;

    public TimeSpan StatusUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    public BreedingSolverResult Solve(
        BreedingSolverRequest request,
        SolverStateController controller
    )
    {
        if (request.Target.RequiredAttacks.Count > 1)
            throw new NotSupportedException("Only one required attack is currently supported");

        var context = SolverRunContext.Create(request, controller);
        var run = new SolverRun(
            context,
            status => StatusUpdated?.Invoke(status),
            StatusUpdateInterval
        );

        return new(
            run.Execute(),
            controller.CancellationToken.IsCancellationRequested
        );
    }
}
