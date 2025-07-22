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
using System.Diagnostics;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
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

        private PalListPresetCollectionViewModel PalListPresets => new(
            CurrentTarget?.PalSource?.Save,
            CurrentTarget?.PalSource?.Selections,
            AppSettings.Current?.PalListPresets
        );

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
            MaxGoldCost = 0;

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

            ChangeSurgeryPassives = new RelayCommand(() =>
            {
                var window = new PassivesCheckListWindow();
                window.DataContext = new PassivesCheckListViewModel(
                    onCancel: null,
                    onSave: (passiveSelections) => BannedSurgeryPassives = passiveSelections.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList(),
                    initialState: PalDB.LoadEmbedded().SurgeryPassiveSkills.ToDictionary(p => p, p => !BannedSurgeryPassives.Contains(p))
                )
                {
                    Title = new HardCodedText("TODO")
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

        private int maxGoldCost;
        public int MaxGoldCost
        {
            get => maxGoldCost;
            set => SetProperty(ref maxGoldCost, Math.Max(value, 0));
        }

        private int maxGenderReversers;
        public int MaxGenderReversers
        {
            get => maxGenderReversers;
            set => SetProperty(ref maxGenderReversers, Math.Max(value, 0));
        }

        [ObservableProperty]
        private bool eagerPruning;

        [ObservableProperty]
        private bool optimizeInitStep;

        private void OnStatePropertiesChanged()
        {
            OnPropertyChanged(nameof(CanRunSolver));
            OnPropertyChanged(nameof(CanCancelSolver));
            OnPropertyChanged(nameof(CanEditSettings));
        }

        private SolverJobViewModel currentJob;
        public SolverJobViewModel CurrentJob
        {
            get => currentJob;
            private set
            {
                if (currentJob != null && currentJob != value)
                {
                    currentJob.PropertyChanged -= CurrentJob_PropertyChanged;
                }

                if (SetProperty(ref currentJob, value))
                {
                    if (currentJob != null)
                    {
                        currentJob.PropertyChanged += CurrentJob_PropertyChanged;
                    }

                    OnStatePropertiesChanged();
                }
            }
        }

        private void CurrentJob_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentJob.CurrentState))
                OnStatePropertiesChanged();
        }

        private PalTargetViewModel currentTarget;
        public PalTargetViewModel CurrentTarget
        {
            get => currentTarget;
            set
            {
                if (currentTarget != value && currentTarget != null)
                    currentTarget.PropertyChanged -= CurrentTarget_PropertyChanged;

                if (SetProperty(ref currentTarget, value))
                {
                    if (currentTarget != null)
                    {
                        currentTarget.PropertyChanged += CurrentTarget_PropertyChanged;
                        CurrentJob = currentTarget.CurrentLatestJob;
                    }
                    else
                    {
                        CurrentJob = null;
                    }

                    OnStatePropertiesChanged();
                }
            }
        }

        private void CurrentTarget_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(currentTarget.IsValid))
                OnPropertyChanged(nameof(CanRunSolver));

            if (e.PropertyName == nameof(currentTarget.CurrentLatestJob))
                CurrentJob = currentTarget.CurrentLatestJob;
        }

        public ICommand RunSolverCommand { get; }
        public ICommand CancelSolverCommand { get; }
        public ICommand PauseSolverCommand { get; }
        public ICommand ResumeSolverCommand { get; }

        public bool CanRunSolver => CurrentTarget?.IsValid == true && CurrentJob?.IsActive != true;
        public bool CanCancelSolver => CurrentJob?.IsActive == true;
        public bool CanEditSettings => CurrentJob?.IsActive != true;

        public IRelayCommand ChangeBredPals { get; }
        public IRelayCommand ChangeWildPals { get; }
        public IRelayCommand ChangeSurgeryPassives { get; }

        [ObservableProperty]
        private List<Pal> bannedBredPals = new List<Pal>();

        [ObservableProperty]
        private List<Pal> bannedWildPals = new List<Pal>();

        [ObservableProperty]
        private List<PassiveSkill> bannedSurgeryPassives = new List<PassiveSkill>();

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
                maxThreads: MaxThreads,

                maxSurgeryCost: MaxGoldCost,
                allowedSurgeryPassives: PalDB.LoadEmbedded().SurgeryPassiveSkills.Except(BannedSurgeryPassives).ToList(),
                maxSurgeryReversers: MaxGenderReversers,

                eagerPruning: EagerPruning,
                optimizeInitStep: OptimizeInitStep
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
            BannedSurgeryPassiveInternalNames = BannedSurgeryPassives.Select(p => p.InternalName).ToList(),
            MaxGoldCost = MaxGoldCost,
            MaxGenderReversers = MaxGenderReversers,
            EagerPruning = EagerPruning,
            OptimizeInitStep = OptimizeInitStep,
        };

        public void CopyFrom(SerializableSolverSettings model)
        {
            MaxBreedingSteps = model.MaxBreedingSteps;
            MaxSolverIterations = model.MaxSolverIterations;
            MaxWildPals = model.MaxWildPals;
            MaxInputIrrelevantPassives = model.MaxInputIrrelevantPassives;
            MaxBredIrrelevantPassives = model.MaxBredIrrelevantPassives;
            MaxThreads = model.MaxThreads;
            MaxGoldCost = model.MaxGoldCost;
            MaxGenderReversers = model.MaxGenderReversers;
            EagerPruning = model.EagerPruning;
            OptimizeInitStep = model.OptimizeInitStep;

            BannedBredPals = model.BannedBredPals(PalDB.LoadEmbedded());
            BannedWildPals = model.BannedWildPals(PalDB.LoadEmbedded());
            BannedSurgeryPassives = model.BannedSurgeryPassives(PalDB.LoadEmbedded());
        }
    }
}
