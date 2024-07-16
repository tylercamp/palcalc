using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View;
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

            ChangeWildPals = new RelayCommand(() =>
            {
                var window = new PalCheckListWindow();
                window.DataContext = new PalCheckListViewModel(
                    onCancel: null,
                    onSave: (palSelections) => BannedWildPals = palSelections.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList(),
                    initialState: PalDB.LoadEmbedded().Pals.ToDictionary(p => p, p => !BannedWildPals.Contains(p))
                ) {
                    Title = LocalizationCodes.LC_SOLVER_SETTINGS_ALLOWED_WILD_PALS.Bind()
                };
                window.Owner = App.Current.MainWindow;
                window.ShowDialog();
            });
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

        public int NumCpus => Environment.ProcessorCount;

        private int maxThreads;
        public int MaxThreads
        {
            get => maxThreads;
            set => SetProperty(ref maxThreads, Math.Clamp(value, 0, NumCpus));
        }

        [ObservableProperty]
        private bool canRunSolver = false;

        [ObservableProperty]
        private bool canCancelSolver = false;

        [ObservableProperty]
        private bool canEditSettings = true;

        public IRelayCommand ChangeWildPals { get; }

        [ObservableProperty]
        private List<Pal> bannedWildPals = new List<Pal>();

        public BreedingSolver ConfiguredSolver(GameSettings gameSettings, List<PalInstance> pals) => new BreedingSolver(
            gameSettings,
            PalDB.LoadEmbedded(),
            PruningRulesBuilder.Default,
            pals,
            MaxBreedingSteps,
            MaxWildPals,
            allowedWildPals: PalDB.LoadEmbedded().Pals.Except(BannedWildPals).ToList(),
            MaxInputIrrelevantTraits,
            MaxBredIrrelevantTraits,
            TimeSpan.MaxValue,
            MaxThreads
        );

        public SolverSettings AsModel => new SolverSettings()
        {
            MaxBreedingSteps = MaxBreedingSteps,
            MaxWildPals = MaxWildPals,
            MaxInputIrrelevantTraits = MaxInputIrrelevantTraits,
            MaxBredIrrelevantTraits = MaxBredIrrelevantTraits,
            MaxThreads = MaxThreads,
            BannedWildPalInternalNames = BannedWildPals.Select(p => p.InternalName).ToList(),
        };

        public static SolverControlsViewModel FromModel(SolverSettings model) => new SolverControlsViewModel()
        {
            MaxBreedingSteps = model.MaxBreedingSteps,
            MaxWildPals = model.MaxWildPals,
            MaxInputIrrelevantTraits = model.MaxInputIrrelevantTraits,
            MaxBredIrrelevantTraits = model.MaxBredIrrelevantTraits,
            MaxThreads = model.MaxThreads,
            
            BannedWildPals = model.BannedWildPals(PalDB.LoadEmbedded()),
        };
    }
}
