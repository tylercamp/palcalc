using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Presets;
using PalCalc.UI.ViewModel.SaveSelection;
using PalCalc.UI.ViewModel.Solver;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI.ViewModel
{
    internal partial class SolverPageViewModel : ObservableObject, IDisposable
    {
        private static SolverPageViewModel designerInstance;
        public static SolverPageViewModel DesignerInstance
        {
            get
            {
                if (designerInstance == null)
                {
                    AppSettings.Current = new AppSettings();
                    designerInstance = new SolverPageViewModel(
                        Dispatcher.CurrentDispatcher,
                        CommonSaveOperationsViewModel.DesignerInstance,
                        SaveGameViewModel.DesignerInstance,
                        new PalTargetListViewModel(new PalSourceViewModel(SaveGameViewModel.DesignerInstance, null))
                    );
                }

                return designerInstance;
            }
        }

        private static ILogger logger = Log.ForContext<SolverPageViewModel>();
        private static PalDB db = PalDB.LoadEmbedded();
        private Dispatcher dispatcher;
        private AppSettings settings;
        private PassiveSkillsPresetCollectionViewModel passivePresets;
        private IRelayCommand<PalSpecifierViewModel> deletePalTargetCommand;
        private ExitEventHandler appExitHandler;
        private CancelEventHandler mainWindowClosingHandler;
        private PropertyChangedEventHandler solverControlsPropertyChangedHandler;
        private Action<PassiveSkillsPresetViewModel> presetSelectedHandler;

        public ICommand RunSolverCommand { get; }
        public ICommand PauseSolverCommand { get; }
        public ICommand ResumeSolverCommand { get; }
        public ICommand CancelSolverCommand { get; }

        public SaveGameViewModel OpenedSave { get; }

        [ObservableProperty]
        private GameSettingsViewModel selectedGameSettings;
        [ObservableProperty]
        private SolverControlsViewModel solverControls;
        [ObservableProperty]
        private PalTargetListViewModel palTargetList;

        [ObservableProperty]
        private bool showNoResultsNotice = false;

        private PalTargetViewModel palTarget;
        public PalTargetViewModel PalTarget
        {
            get => palTarget;
            set
            {
                var oldValue = PalTarget;
                if (SetProperty(ref palTarget, value))
                {
                    var currentResults = PalTarget?.CurrentPalSpecifier?.CurrentResults;
                    ShowNoResultsNotice = currentResults != null && currentResults.Results?.Count == 0;

                    SolverControls.CurrentTarget = palTarget;
                }
            }
        }

        public SolverQueueViewModel SolverQueue { get; } = new SolverQueueViewModel();

        public CommonSaveOperationsViewModel SaveOperations { get; }

        // main app model
        public SolverPageViewModel(Dispatcher dispatcher, CommonSaveOperationsViewModel saveOperations, SaveGameViewModel selectedSave, PalTargetListViewModel targets)
        {
            this.dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            OpenedSave = selectedSave;

            SaveOperations = saveOperations.WithNavigateCondition(() => SolverQueue.QueuedItems.Count == 0);

            settings = AppSettings.Current;
            settings.SolverSettings ??= new SerializableSolverSettings();
            passivePresets = new PassiveSkillsPresetCollectionViewModel(settings.PassiveSkillsPresets);

            PauseSolverCommand = new RelayCommand(PauseSolver);
            ResumeSolverCommand = new RelayCommand(ResumeSolver);
            CancelSolverCommand = new RelayCommand(CancelSolver);

            RunSolverCommand = new RelayCommand(RunSolver);

            this.dispatcher.Invoke(() =>
            {
                // (needed for XAML designer view)
                if (App.Current.MainWindow != null)
                {
                    appExitHandler = (o, e) =>
                    {
                        foreach (var target in SolverQueue.QueuedItems)
                            target.LatestJob.Cancel();
                    };
                    App.Current.Exit += appExitHandler;

                    mainWindowClosingHandler = (o, e) =>
                    {
                        if (SolverQueue.QueuedItems.Count == 0)
                            return;

                        var title = LocalizationCodes.LC_JOB_QUEUE_CLOSING_TITLE.Bind().Value;
                        var msg = LocalizationCodes.LC_JOB_QUEUE_CLOSING_MSG.Bind().Value;
                        if (AdonisMessageBox.Show(App.Current.MainWindow, msg, title, AdonisMessageBoxButton.YesNo) == AdonisMessageBoxResult.No)
                        {
                            e.Cancel = true;
                        }
                    };
                    App.Current.MainWindow.Closing += mainWindowClosingHandler;
                }
            });

            PalTargetList = targets;
            PalTargetList.SourcePals.PropertyChanged += SourcePals_PropertyChanged;

            SolverControls = new SolverControlsViewModel(
                sourcePals: PalTargetList.SourcePals,
                runSolverCommand: RunSolverCommand,
                cancelSolverCommand: CancelSolverCommand,
                pauseSolverCommand: PauseSolverCommand,
                resumeSolverCommand: ResumeSolverCommand
            );
            SolverControls.CopyFrom(settings.SolverSettings);
            solverControlsPropertyChangedHandler = SolverControls_PropertyChanged;
            SolverControls.PropertyChanged += solverControlsPropertyChangedHandler;
            
            // TODO - would prefer to have the delete command managed by the target list, rather than having
            //        to manually assign the command for each specifier VM
            deletePalTargetCommand = new RelayCommand<PalSpecifierViewModel>(OnDeletePalSpecifier);
            foreach (var target in targets.Targets.Where(t => !t.IsReadOnly))
                target.DeleteCommand = deletePalTargetCommand;

            Storage.SaveReloaded += Storage_SaveReloaded;
            CachedSaveGame.SaveFileLoadEnd += CachedSaveGame_SaveFileLoadEnd;

            this.dispatcher.Invoke(UpdateFromSaveProperties);

            presetSelectedHandler = PassivePresets_PresetSelected;
            passivePresets.PresetSelected += presetSelectedHandler;

            SolverQueue.SelectItemCommand = new RelayCommand<PalSpecifierViewModel>(vm => PalTargetList.SelectedTarget = PalTargetList.Targets.FirstOrDefault(t => t.LatestJob == vm.LatestJob));
            ((INotifyCollectionChanged)SolverQueue.QueuedItems).CollectionChanged += (a, b) =>
            {
                SaveOperations.NavigateSaveSelectionPageCommand.NotifyCanExecuteChanged();
            };
        }

        public void Dispose()
        {
            Storage.SaveReloaded -= Storage_SaveReloaded;
            CachedSaveGame.SaveFileLoadEnd -= CachedSaveGame_SaveFileLoadEnd;

            if (PalTargetList != null)
            {
                PalTargetList.SourcePals.PropertyChanged -= SourcePals_PropertyChanged;
                PalTargetList.PropertyChanged -= PalTargetList_PropertyChanged;
                PalTargetList.OrderChanged -= SaveTargetList;
            }

            if (SelectedGameSettings != null)
                SelectedGameSettings.PropertyChanged -= GameSettings_PropertyChanged;

            if (SolverControls != null && solverControlsPropertyChangedHandler != null)
                SolverControls.PropertyChanged -= solverControlsPropertyChangedHandler;

            if (passivePresets != null && presetSelectedHandler != null)
                passivePresets.PresetSelected -= presetSelectedHandler;

            if (appExitHandler != null)
                App.Current.Exit -= appExitHandler;

            if (mainWindowClosingHandler != null && App.Current.MainWindow != null)
                App.Current.MainWindow.Closing -= mainWindowClosingHandler;
        }

        private void CachedSaveGame_SaveFileLoadEnd(ISaveGame save, CachedSaveGame cachedSave)
        {
            if (save != OpenedSave?.Value || cachedSave == null) return;
            PalTargetList.UpdateCachedData(cachedSave, GameSettingsViewModel.Load(save).ModelObject);
        }

        private void SolverControls_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            settings.SolverSettings = SolverControls.AsModel;
            Storage.SaveAppSettings(settings);
        }

        private void SourcePals_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalSourceViewModel.AvailablePals))
                SaveTargetList(PalTargetList);
        }

        private void PassivePresets_PresetSelected(PassiveSkillsPresetViewModel selectedPreset)
        {
            var spec = PalTarget?.CurrentPalSpecifier;
            if (spec != null) selectedPreset.ApplyTo(spec);
        }

        private void Storage_SaveReloaded(ISaveGame save)
        {
            if (OpenedSave?.Value == save)
            {
                UpdateFromSaveProperties();
            }
        }

        private void OnDeletePalSpecifier(PalSpecifierViewModel spec)
        {
            if (spec == null) return;

            if (!PalTargetList.Targets.Contains(spec))
            {
                return;
            }

            var title = LocalizationCodes.LC_DELETE_PAL_TARGET_TITLE.Bind().Value;
            var msg = LocalizationCodes.LC_DELETE_PAL_TARGET_MSG.Bind(spec.TargetPal.Name).Value;

            if (AdonisMessageBox.Show(App.Current.MainWindow, msg, title, AdonisMessageBoxButton.YesNo) == AdonisMessageBoxResult.Yes)
            {
                PalTargetList.Remove(spec);
                SaveTargetList(PalTargetList);
                var dataPath = Path.Join(Storage.SaveFileTargetsDataPath(OpenedSave.Value), $"{spec.Id}.json");
                if (File.Exists(dataPath))
                    File.Delete(dataPath);

                UpdatePalTarget();
            }
        }

        private void UpdateFromSaveProperties()
        {
            if (PalTargetList != null)
            {
                PalTargetList.PropertyChanged -= PalTargetList_PropertyChanged;
                PalTargetList.OrderChanged -= SaveTargetList;
            }

            if (SelectedGameSettings != null)
                SelectedGameSettings.PropertyChanged -= GameSettings_PropertyChanged;

            PalTargetList.PropertyChanged += PalTargetList_PropertyChanged;
            PalTargetList.OrderChanged += SaveTargetList; // TODO - debounce

            SelectedGameSettings = GameSettingsViewModel.Load(OpenedSave.Value);
            SelectedGameSettings.PropertyChanged += GameSettings_PropertyChanged;

            UpdatePalTarget();
            UpdateSolverControls();

            var csg = OpenedSave.CachedValue;
            foreach (var passive in csg.OwnedPals.SelectMany(p => p.PassiveSkills).OfType<UnrecognizedPassiveSkill>())
            {
                // preload any unrecognized passives so they appear in dropdowns
                PassiveSkillViewModel.Make(passive);
            }
        }

        private void UpdatePalTarget()
        {
            if (PalTargetList?.SelectedTarget != null)
            {
                PalTarget = new PalTargetViewModel(OpenedSave, PalTargetList.SourcePals, PalTargetList.SelectedTarget, passivePresets);
                passivePresets.ActivePalTarget = PalTarget;
            }
            else
                PalTarget = null;
        }

        private void GameSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var settings = sender as GameSettingsViewModel;
            settings.Save(OpenedSave.Value);
        }

        private void PalTargetList_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalTargetList.SelectedTarget))
            {
                UpdatePalTarget();
            }
        }

        private void SaveTargetList(PalTargetListViewModel list)
        {
            if (Storage.DEBUG_DisableStorage) return;

            var outputFolder = Storage.SaveFileDataPath(OpenedSave.Value);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var outputFile = Path.Join(outputFolder, "pal-target-ids.json");
            var converter = new PalTargetListViewModelConverter(db, new GameSettings(), OpenedSave, OpenedSave.CachedValue, list.Targets.Where(t => !t.IsReadOnly).ToDictionary(t => t.Id));
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(list, converter));
        }

        public void SaveTarget(PalSpecifierViewModel item)
        {
            if (Storage.DEBUG_DisableStorage) return;

            var outputFolder = Storage.SaveFileTargetsDataPath(OpenedSave.Value);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var outputFile = Path.Join(outputFolder, $"{item.Id}.json");
            var converter = new PalSpecifierViewModelConverter(db, SelectedGameSettings.ModelObject, OpenedSave.CachedValue);
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(item, converter));
        }

        private void RunSolver()
        {
            var currentSpec = PalTarget?.CurrentPalSpecifier;
            if (currentSpec?.ModelObject == null) return;

            var cachedData = OpenedSave.CachedValue;
            if (cachedData == null) return;

            var initialSpec = PalTarget.InitialPalSpecifier;

            if (initialSpec == null)
            {
                initialSpec = currentSpec.Copy();
                initialSpec.DeleteCommand = deletePalTargetCommand;

                PalTargetList.Add(initialSpec);
                PalTargetList.SelectedTarget = initialSpec;
                SaveTargetList(PalTargetList);
                SaveTarget(initialSpec);

                UpdatePalTarget();
                currentSpec = PalTarget.CurrentPalSpecifier;
            }

            var originalSolverSettings = SolverControls.AsModel;
            var originalGameSettings = SelectedGameSettings.ModelObject;
            var job = new SolverJobViewModel(
                dispatcher,
                SolverControls.ConfiguredSolver(originalGameSettings, PalTargetList.SourcePals.AvailablePals.ToList()),
                currentSpec,
                cachedData.StateId
            );

            job.JobCompleted += (job) =>
            {
                currentSpec.CurrentResults = new BreedingResultListViewModel()
                {
                    Results = job.Results.Select(r => new BreedingResultViewModel(cachedData, originalGameSettings, r)).ToList(),
                    SettingsSnapshot = new BreedingResultListViewModelSettingsSnapshot()
                    {
                        GameSettings = originalGameSettings,
                        SolverSettings = originalSolverSettings
                    }
                };

                if (job.SaveStateId != cachedData.StateId)
                {
                    var latestGameSettings = GameSettingsViewModel.Load(OpenedSave.Value).ModelObject;
                    currentSpec.CurrentResults.UpdateCachedData(cachedData, latestGameSettings);
                }

                var shouldUpdateTarget = PalTargetList.SelectedTarget == initialSpec;

                PalTargetList.Replace(initialSpec, currentSpec);

                if (shouldUpdateTarget)
                {
                    PalTargetList.SelectedTarget = currentSpec;
                    ShowNoResultsNotice = (job.Results.Count == 0);

                    UpdatePalTarget();
                }

                SaveTarget(currentSpec);
                SaveTargetList(PalTargetList);
            };

            job.JobCancelled += (job) =>
            {
                initialSpec.LatestJob = null;
                currentSpec.LatestJob = null;
            };

            // initialSpec is the original target stored in the pal target list; assign latest job so it can show busy/paused/idle state
            initialSpec.LatestJob = job;
            // currentSpec is the currently-configured target; if/when it completes it should keep a copy of its job so the run info
            // can be displayed (progress bar, "total breeding pairs processed" info, etc.)
            currentSpec.LatestJob = job;

            SolverQueue.Run(currentSpec);
        }

        private void CancelSolver()
        {
            PalTargetList.SelectedTarget?.LatestJob?.Cancel();
        }

        private void PauseSolver()
        {
            PalTargetList.SelectedTarget?.LatestJob?.Pause();
        }

        private void ResumeSolver()
        {
            PalTargetList.SelectedTarget?.LatestJob?.Run();
        }

        
        private void UpdateSolverControls()
        {
            SolverControls.CurrentTarget = PalTarget;
        }
    }
}
