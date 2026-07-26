using PalCalc.Model;

namespace PalCalc.Solver;

/// <summary>
/// The fixed definition of one solver invocation.
/// </summary>
public sealed class BreedingSolverRequest
{
    private readonly PalSpecifier target;

    public BreedingSolverRequest(PalSpecifier target, BreedingSolverSettings settings)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);

        this.target = target.NormalizedCopy();
        Settings = settings;
    }

    /// <summary>
    /// Returns a copy of the normalized target captured by this request.
    /// </summary>
    public PalSpecifier Target => target.NormalizedCopy();

    public BreedingSolverSettings Settings { get; }

    internal PalSpecifier CapturedTarget => target;
}

internal sealed class SolverRunContext
{
    private SolverRunContext(
        PalSpecifier target,
        BreedingSolverSettings settings,
        BreedingMechanics mechanics,
        PalBreedingDB breedingDB,
        SolverStateController controller,
        ICandidateSelectionPolicy selectionPolicy
    )
    {
        Target = target;
        Settings = settings;
        Mechanics = mechanics;
        BreedingDB = breedingDB;
        Controller = controller;
        SelectionPolicy = selectionPolicy;
    }

    public PalSpecifier Target { get; }
    public BreedingSolverSettings Settings { get; }
    public BreedingMechanics Mechanics { get; }
    public PalBreedingDB BreedingDB { get; }
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
            request.Settings.ResultPruning,
            controller.CancellationToken
        );

        return new(
            target: request.CapturedTarget,
            settings: request.Settings,
            // Mechanics is immutable. Capturing the current PalDB-owned value
            // makes replacing it affect later runs without changing this run.
            mechanics: request.Settings.DB.BreedingMechanics,
            breedingDB: request.Settings.BreedingDB,
            controller: controller,
            selectionPolicy: selectionPolicy
        );
    }
}
