using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel.Presets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PalCalc.UI.ViewModel.Solver
{
    public enum SolverState
    {
        Idle,
        Running,
        Paused
    }

    public partial class SolverControlsViewModel : ObservableObject
    {
        public static SolverControlsViewModel DesignerInstance { get; } = new SolverControlsViewModel(null, null, null, null);

        private PalListPresetCollectionViewModel PalListPresets => new(AppSettings.Current?.PalListPresets);

        public SolverControlsViewModel(
            ICommand runSolverCommand,
            ICommand cancelSolverCommand,
            ICommand pauseSolverCommand,
            ICommand resumeSolverCommand
        )
        {
            MaxBreedingSteps = 6;
            MaxWildPals = 1;
            MaxInputIrrelevantPassives = 2;
            MaxBredIrrelevantPassives = 1;

            ChangeBredPals = new RelayCommand(() =>
            {
                var window = new PalCheckListWindow();
                window.DataContext = new PalCheckListViewModel(
                    presets: PalListPresets,
                    onCancel: null,
                    onSave: (palSelections) => BannedBredPals = palSelections.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList(),
                    initialState: PalDB.LoadEmbedded().Pals.ToDictionary(p => p, p => !BannedBredPals.Contains(p))
                )
                {
                    Title = LocalizationCodes.LC_SOLVER_SETTINGS_ALLOWED_BRED_PALS.Bind()
                };
                window.Owner = App.Current.MainWindow;
                window.ShowDialog();
            });

            ChangeWildPals = new RelayCommand(() =>
            {
                var window = new PalCheckListWindow();
                window.DataContext = new PalCheckListViewModel(
                    presets: PalListPresets,
                    onCancel: null,
                    onSave: (palSelections) => BannedWildPals = palSelections.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList(),
                    initialState: PalDB.LoadEmbedded().Pals.ToDictionary(p => p, p => !BannedWildPals.Contains(p))
                )
                {
                    Title = LocalizationCodes.LC_SOLVER_SETTINGS_ALLOWED_WILD_PALS.Bind()
                };
                window.Owner = App.Current.MainWindow;
                window.ShowDialog();
            });

            RunSolverCommand = runSolverCommand;
            CancelSolverCommand = cancelSolverCommand;
            PauseSolverCommand = pauseSolverCommand;
            ResumeSolverCommand = resumeSolverCommand;
        }

        private int maxBreedingSteps;
        public int MaxBreedingSteps
        {
            get => maxBreedingSteps;
            set
            {
                if (SetProperty(ref maxBreedingSteps, Math.Max(1, value)))
                {
                    // try to keep "max solver iterations" capable of reaching the requested number of breeding steps
                    if (MaxSolverIterations < MaxBreedingSteps)
                        MaxSolverIterations = MaxBreedingSteps;
                }
            }
        }

        private int maxWildPals;
        public int MaxWildPals
        {
            get => maxWildPals;
            set => SetProperty(ref maxWildPals, Math.Max(0, value));
        }

        private int maxInputIrrelevantPassives;
        public int MaxInputIrrelevantPassives
        {
            get => maxInputIrrelevantPassives;
            set => SetProperty(ref maxInputIrrelevantPassives, Math.Clamp(value, 0, 4));
        }

        private int maxBredIrrelevantPassives;
        public int MaxBredIrrelevantPassives
        {
            get => maxBredIrrelevantPassives;
            set => SetProperty(ref maxBredIrrelevantPassives, Math.Clamp(value, 0, 4));
        }

        public int NumCpus => Environment.ProcessorCount;

        private int maxThreads;
        public int MaxThreads
        {
            get => maxThreads;
            set => SetProperty(ref maxThreads, Math.Clamp(value, 0, NumCpus));
        }

        private int maxSolverIterations;
        public int MaxSolverIterations
        {
            get => maxSolverIterations;
            set => SetProperty(ref maxSolverIterations, Math.Clamp(value, 1, 99));
        }

        [NotifyPropertyChangedFor(nameof(CanRunSolver))]
        [ObservableProperty]
        private bool isValidConfig;

        [NotifyPropertyChangedFor(nameof(CanRunSolver))]
        [NotifyPropertyChangedFor(nameof(CanCancelSolver))]
        [NotifyPropertyChangedFor(nameof(CanEditSettings))]
        [ObservableProperty]
        private SolverState currentSolverState;

        public ICommand RunSolverCommand { get; }
        public ICommand CancelSolverCommand { get; }
        public ICommand PauseSolverCommand { get; }
        public ICommand ResumeSolverCommand { get; }

        public bool CanRunSolver => IsValidConfig && CurrentSolverState == SolverState.Idle;
        public bool CanCancelSolver => CurrentSolverState != SolverState.Idle;
        public bool CanEditSettings => CurrentSolverState == SolverState.Idle;

        public IRelayCommand ChangeBredPals { get; }
        public IRelayCommand ChangeWildPals { get; }

        [ObservableProperty]
        private List<Pal> bannedBredPals = new List<Pal>();

        [ObservableProperty]
        private List<Pal> bannedWildPals = new List<Pal>();

        public BreedingSolver ConfiguredSolver(GameSettings gameSettings, List<PalInstance> pals) => new BreedingSolver(
            new BreedingSolverSettings(
                db: PalDB.LoadEmbedded(),
                gameSettings: gameSettings,
                pruningBuilder: PruningRulesBuilder.Default,
                ownedPals: pals,
                maxBreedingSteps: MaxBreedingSteps,
                maxSolverIterations: MaxSolverIterations,
                maxWildPals: MaxWildPals,
                allowedWildPals: PalDB.LoadEmbedded().Pals.Except(BannedWildPals).ToList(),
                bannedBredPals: BannedBredPals,
                maxInputIrrelevantPassives: MaxInputIrrelevantPassives,
                maxBredIrrelevantPassives: MaxBredIrrelevantPassives,
                maxEffort: TimeSpan.MaxValue,
                maxThreads: MaxThreads
            )
        );

        public SerializableSolverSettings AsModel => new SerializableSolverSettings()
        {
            MaxBreedingSteps = MaxBreedingSteps,
            MaxSolverIterations = MaxSolverIterations,
            MaxWildPals = MaxWildPals,
            MaxInputIrrelevantPassives = MaxInputIrrelevantPassives,
            MaxBredIrrelevantPassives = MaxBredIrrelevantPassives,
            MaxThreads = MaxThreads,
            BannedBredPalInternalNames = BannedBredPals.Select(p => p.InternalName).ToList(),
            BannedWildPalInternalNames = BannedWildPals.Select(p => p.InternalName).ToList(),
        };

        public void CopyFrom(SerializableSolverSettings model)
        {
            MaxBreedingSteps = model.MaxBreedingSteps;
            MaxSolverIterations = model.MaxSolverIterations;
            MaxWildPals = model.MaxWildPals;
            MaxInputIrrelevantPassives = model.MaxInputIrrelevantPassives;
            MaxBredIrrelevantPassives = model.MaxBredIrrelevantPassives;
            MaxThreads = model.MaxThreads;

            BannedBredPals = model.BannedBredPals(PalDB.LoadEmbedded());
            BannedWildPals = model.BannedWildPals(PalDB.LoadEmbedded());
        }
    }
}
