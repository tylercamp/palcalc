using PalCalc.Model;
using PalCalc.Solver.Processing.Attacks;
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
        PalSpecifier breedingTarget,
        BreedingSolverSettings settings,
        BreedingMechanics mechanics,
        PalBreedingDB breedingDB,
        SolverStateController controller,
        ICandidateSelectionPolicy selectionPolicy,
        AttackTargetContext attackTargets
    )
    {
        Target = target;
        BreedingTarget = breedingTarget;
        Settings = settings;
        Mechanics = mechanics;
        BreedingDB = breedingDB;
        Controller = controller;
        SelectionPolicy = selectionPolicy;
        AttackTargets = attackTargets;
    }

    // The final target pal
    public PalSpecifier Target { get; }

    // The effective target pal used for most of the breeding process, may differ
    // slightly from Target depending on settings (see `TargetDuringBreeding`)
    public PalSpecifier BreedingTarget { get; }

    public BreedingSolverSettings Settings { get; }
    public BreedingMechanics Mechanics { get; }
    public PalBreedingDB BreedingDB { get; }
    public SolverStateController Controller { get; }
    public ICandidateSelectionPolicy SelectionPolicy { get; }
    public AttackTargetContext AttackTargets { get; }

    public static SolverRunContext Create(
        BreedingSolverRequest request,
        SolverStateController controller,
        ICandidateSelectionPolicy selectionPolicy = null
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(controller);

        var target = request.CapturedTarget;
        var settings = request.Settings;
        var breedingTarget = TargetDuringBreeding(target, settings);
        // If surgery leaves no desired passives to breed for, cakes can only help with attacks.
        // Use the breeding target here so deferred surgery passives don't enable extra cake options.
        if (settings.SpecialCakePolicy == SpecialCakePolicy.AttacksAndPassives && !breedingTarget.DesiredPassives.Any())
        {
            settings = settings.WithAttackOnlyCakes();
        }

        var attackTargets = new AttackTargetContext(target, settings.DB);
        selectionPolicy ??= new DefaultCandidateSelectionPolicy(
            settings.ResultPruning,
            controller.CancellationToken,
            attackTargets: attackTargets,
            considerPassiveCakes: settings.SpecialCakePolicy == SpecialCakePolicy.AttacksAndPassives
        );

        return new(
            target: target,
            breedingTarget: breedingTarget,
            settings: settings,
            // Mechanics is immutable. Capturing the current PalDB-owned value
            // makes replacing it affect later runs without changing this run.
            mechanics: settings.DB.BreedingMechanics,
            breedingDB: settings.BreedingDB,
            controller: controller,
            selectionPolicy: selectionPolicy,
            attackTargets: attackTargets
        );
    }

    private static PalSpecifier TargetDuringBreeding(PalSpecifier target, BreedingSolverSettings settings)
    {
        if (!settings.SkipSurgeryPassivesDuringSearch)
            return target;

        // If some passives are available through surgery, and the max-cost can cover all selected
        // passives, we can drop these passives from the main search process and let
        // the surgery post-pass add them at the very end instead.
        // (don't ignore passives with breeding effects)

        var selectedSurgeryPassives = target.RequiredPassives
            .Where(passive => passive.SupportsSurgery && settings.SurgeryPassives.Contains(passive))
            .ToList();
        if (selectedSurgeryPassives.Count == 0 ||
            selectedSurgeryPassives.Sum(passive => passive.SurgeryCost) > settings.MaxSurgeryCost)
            return target;

        var deferred = selectedSurgeryPassives.Where(passive => passive.TrackedEffects.Count == 0);
        var breedingTarget = target.NormalizedCopy();
        breedingTarget.RequiredPassives = breedingTarget.RequiredPassives.Except(deferred).ToList();
        return breedingTarget;
    }
}
