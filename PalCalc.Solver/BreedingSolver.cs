using PalCalc.Solver.PalReference;

namespace PalCalc.Solver;

public sealed class BreedingSolver
{
    public event Action<SolverStatus> SolverStateUpdated;

    public TimeSpan SolverStateUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    public List<IPalReference> Solve(
        BreedingSolverRequest request,
        SolverStateController controller
    )
    {
        var context = SolverRunContext.Create(request, controller);
        var run = new SolverRun(
            context,
            status => SolverStateUpdated?.Invoke(status),
            SolverStateUpdateInterval
        );

        return run.Execute();
    }
}
