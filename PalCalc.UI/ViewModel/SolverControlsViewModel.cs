using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public partial class SolverControlsViewModel : ObservableObject
    {
        public SolverControlsViewModel()
        {
            MaxBreedingSteps = 6;
            MaxWildPals = 1;
            MaxInputIrrelevantTraits = 2;
            MaxBredIrrelevantTraits = 1;
        }

        private int maxBreedingSteps;
        public int MaxBreedingSteps
        {
            get => maxBreedingSteps;
            set => SetProperty(ref maxBreedingSteps, Math.Max(1, value));
        }

        private int maxWildPals;
        public int MaxWildPals
        {
            get => maxWildPals;
            set => SetProperty(ref maxWildPals, Math.Max(0, value));
        }

        private int maxInputIrrelevantTraits;
        public int MaxInputIrrelevantTraits
        {
            get => maxInputIrrelevantTraits;
            set => SetProperty(ref maxInputIrrelevantTraits, Math.Clamp(value, 0, 4));
        }

        public int maxBredIrrelevantTraits;
        public int MaxBredIrrelevantTraits
        {
            get => maxBredIrrelevantTraits;
            set => SetProperty(ref maxBredIrrelevantTraits, Math.Clamp(value, 0, 4));
        }

        [ObservableProperty]
        private bool canRunSolver = false;

        [ObservableProperty]
        private bool canCancelSolver = false;

        [ObservableProperty]
        private bool canEditSettings = true;

        public BreedingSolver ConfiguredSolver(GameSettings gameSettings, List<PalInstance> pals) => new BreedingSolver(
            gameSettings,
            PalDB.LoadEmbedded(),
            pals,
            MaxBreedingSteps,
            MaxWildPals,
            MaxInputIrrelevantTraits,
            MaxBredIrrelevantTraits,
            TimeSpan.MaxValue
        );

        public SolverSettings AsModel => new SolverSettings()
        {
            MaxBreedingSteps = MaxBreedingSteps,
            MaxWildPals = MaxWildPals,
            MaxInputIrrelevantTraits = MaxInputIrrelevantTraits,
            MaxBredIrrelevantTraits = MaxBredIrrelevantTraits,
        };

        public static SolverControlsViewModel FromModel(SolverSettings model) => new SolverControlsViewModel()
        {
            MaxBreedingSteps = model.MaxBreedingSteps,
            MaxWildPals = model.MaxWildPals,
            MaxInputIrrelevantTraits = model.MaxInputIrrelevantTraits,
            MaxBredIrrelevantTraits = model.MaxBredIrrelevantTraits,
        };
    }
}
