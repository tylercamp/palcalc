using PalCalc.Model;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver;

/// <summary>
/// Captures the target and settings for one solver invocation. The target is
/// normalized and copied so later caller changes cannot alter the run.
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
