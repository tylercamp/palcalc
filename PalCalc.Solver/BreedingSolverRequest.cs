using PalCalc.Model;

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
        BreedingMechanics mechanics,
        SolverStateController controller,
        ICandidateSelectionPolicy selectionPolicy
    )
    {
        Target = target;
        Settings = settings;
        Mechanics = mechanics;
        Controller = controller;
        SelectionPolicy = selectionPolicy;
    }

    public PalSpecifier Target { get; }
    public BreedingSolverSettings Settings { get; }
    public BreedingMechanics Mechanics { get; }
    public SolverStateController Controller { get; }
    public ICandidateSelectionPolicy SelectionPolicy { get; }

    public static SolverRunContext Create(
        BreedingSolverRequest request,
        SolverStateController controller,
        ICandidateSelectionPolicy selectionPolicy = null
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(controller);

        selectionPolicy ??= new DefaultCandidateSelectionPolicy(
            request.Settings.PruningBuilder,
            controller.CancellationToken
        );

        return new(
            target: request.Target,
            settings: request.Settings,
            // Mechanics is immutable. Capturing the current PalDB-owned value
            // makes replacing it affect later runs without changing this run.
            mechanics: request.Settings.DB.BreedingMechanics,
            controller: controller,
            selectionPolicy: selectionPolicy
        );
    }
}
