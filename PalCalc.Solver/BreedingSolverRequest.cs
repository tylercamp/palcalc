namespace PalCalc.Solver;

/// <summary>
/// The fixed definition of one solver invocation.
/// </summary>
public sealed class BreedingSolverRequest
{
    public BreedingSolverRequest(PalSpecifier target, BreedingSolverSettings settings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);

        Target = target.NormalizedCopy();
        Settings = settings;
    }

    internal PalSpecifier Target { get; }
    internal BreedingSolverSettings Settings { get; }
}

internal sealed class SolverRunContext
{
    private SolverRunContext(
        PalSpecifier target,
        BreedingSolverSettings settings,
        SolverStateController controller
    )
    {
        Target = target;
        Settings = settings;
        Controller = controller;
        StateKeyProvider = DefaultBreedingStateKeyProvider.Instance;
    }

    public PalSpecifier Target { get; }
    public BreedingSolverSettings Settings { get; }
    public SolverStateController Controller { get; }
    public IBreedingStateKeyProvider StateKeyProvider { get; }

    public static SolverRunContext Create(
        BreedingSolverRequest request,
        SolverStateController controller
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(controller);

        return new(
            target: request.Target,
            settings: request.Settings,
            controller: controller
        );
    }
}
