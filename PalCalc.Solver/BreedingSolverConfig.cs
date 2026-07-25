using PalCalc.Model;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver;

public class SolverStateController
{
    public CancellationToken CancellationToken { get; set; }
    public bool IsPaused { get; private set; }

    public void Pause() => IsPaused = true;
    public void Resume() => IsPaused = false;

    internal void PauseIfRequested()
    {
        while (IsPaused) Thread.Sleep(10);
    }
}

public sealed class BreedingSolverSettings
{
    /// <summary>
    /// Creates a settings snapshot. Mutable configuration collections are
    /// copied; model objects are treated as shared, read-only domain objects.
    /// </summary>
    public BreedingSolverSettings(
        PalDB db,
        GameSettings gameSettings,
        IEnumerable<PalInstance> ownedPals,
        PruningRulesBuilder pruningBuilder,
        int maxBreedingSteps,
        int maxSolverIterations,
        int maxWildPals,
        IEnumerable<Pal> allowedWildPals,
        IEnumerable<Pal> bannedBredPals,
        int maxInputIrrelevantPassives,
        int maxBredIrrelevantPassives,
        TimeSpan maxEffort,
        int maxThreads,
        int maxSurgeryCost,
        IEnumerable<PassiveSkill> allowedSurgeryPassives,
        bool useGenderReversers
    )
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(gameSettings);
        ArgumentNullException.ThrowIfNull(ownedPals);
        ArgumentNullException.ThrowIfNull(pruningBuilder);
        ArgumentNullException.ThrowIfNull(allowedWildPals);
        ArgumentNullException.ThrowIfNull(bannedBredPals);
        ArgumentNullException.ThrowIfNull(allowedSurgeryPassives);

        DB = db;
        GameSettings = gameSettings;
        OwnedPals = ownedPals.Where(p => p.Gender != PalGender.NONE).ToList();
        PruningBuilder = pruningBuilder;
        MaxBreedingSteps = maxBreedingSteps;
        MaxSolverIterations = maxSolverIterations;
        MaxWildPals = maxWildPals;
        AllowedWildPals = allowedWildPals.ToList();
        BannedBredPals = bannedBredPals.ToList();
        MaxInputIrrelevantPassives = Math.Clamp(
            maxInputIrrelevantPassives,
            0,
            GameConstants.MaxTotalPassives
        );
        MaxBredIrrelevantPassives = Math.Clamp(
            maxBredIrrelevantPassives,
            0,
            GameConstants.MaxTotalPassives
        );
        MaxEffort = maxEffort;
        MaxThreads = maxThreads <= 0
            ? Environment.ProcessorCount
            : Math.Clamp(maxThreads, 1, Environment.ProcessorCount);
        MaxSurgeryCost = maxSurgeryCost;
        SurgeryPassives = allowedSurgeryPassives.ToList();
        UseGenderReversers = useGenderReversers;
    }

    public PalDB DB { get; }
    public GameSettings GameSettings { get; }
    public IReadOnlyList<PalInstance> OwnedPals { get; }
    public PruningRulesBuilder PruningBuilder { get; }
    public int MaxBreedingSteps { get; }
    public int MaxSolverIterations { get; }
    public int MaxWildPals { get; }
    public IReadOnlyList<Pal> AllowedWildPals { get; }
    public IReadOnlyList<Pal> BannedBredPals { get; }
    public int MaxInputIrrelevantPassives { get; }
    public int MaxBredIrrelevantPassives { get; }
    public TimeSpan MaxEffort { get; }
    public int MaxThreads { get; }
    public int MaxSurgeryCost { get; }
    public IReadOnlyList<PassiveSkill> SurgeryPassives { get; }
    public bool UseGenderReversers { get; }
}
