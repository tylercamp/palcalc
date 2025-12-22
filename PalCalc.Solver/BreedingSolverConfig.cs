using PalCalc.Model;
using PalCalc.Solver.ResultPruning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
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

    public class BreedingSolverSettings(
        PalDB db,
        GameSettings gameSettings,
        List<PalInstance> ownedPals,

        PruningRulesBuilder pruningBuilder,

        int maxBreedingSteps,
        int maxSolverIterations,
        int maxWildPals,

        List<Pal> allowedWildPals,
        List<Pal> bannedBredPals,

        int maxInputIrrelevantPassives,
        int maxBredIrrelevantPassives,

        TimeSpan maxEffort,
        int maxThreads,

        int maxSurgeryCost,
        List<PassiveSkill> allowedSurgeryPassives,
        bool useGenderReversers
    )
    {
        public PalDB DB => db;
        public GameSettings GameSettings => gameSettings;
        public List<PalInstance> OwnedPals { get; } = ownedPals.Where(p => p.Gender != PalGender.NONE).ToList();

        public PruningRulesBuilder PruningBuilder => pruningBuilder;

        public int MaxBreedingSteps => maxBreedingSteps;
        public int MaxSolverIterations => maxSolverIterations;
        public int MaxWildPals => maxWildPals;

        public List<Pal> AllowedWildPals => allowedWildPals;
        public List<Pal> BannedBredPals => bannedBredPals;

        public int MaxInputIrrelevantPassives { get; } = Math.Clamp(maxInputIrrelevantPassives, 0, GameConstants.MaxTotalPassives);
        public int MaxBredIrrelevantPassives { get; } = Math.Clamp(maxBredIrrelevantPassives, 0, GameConstants.MaxTotalPassives);

        public TimeSpan MaxEffort => maxEffort;
        public int MaxThreads { get; } = maxThreads <= 0 ? Environment.ProcessorCount : Math.Clamp(maxThreads, 1, Environment.ProcessorCount);

        // Surgery settings
        public int MaxSurgeryCost => maxSurgeryCost;
        public List<PassiveSkill> SurgeryPassives => allowedSurgeryPassives ?? [];
        public bool UseGenderReversers => useGenderReversers;
    }
}
