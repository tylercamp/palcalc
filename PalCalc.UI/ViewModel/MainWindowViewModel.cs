using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel.Mapped;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<MainWindowViewModel>();
        private static PalDB db = PalDB.LoadEmbedded();
        private Dictionary<SaveGame, PalTargetListViewModel> targetsBySaveFile;
        private LoadingSaveFileModal loadingSaveModal = null;
        private Dispatcher dispatcher;
        private CancellationTokenSource solverTokenSource;
        private AppSettings settings;

        // https://stackoverflow.com/a/73181682
        private static void AllowUIToUpdate()
        {
            DispatcherFrame frame = new();
            // DispatcherPriority set to Input, the highest priority
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Input, new DispatcherOperationCallback(delegate (object parameter)
            {
                frame.Continue = false;
                Thread.Sleep(20); // Stop all processes to make sure the UI update is perform
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
            // DispatcherPriority set to Input, the highest priority
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Input, new Action(delegate { }));
        }

        public MainWindowViewModel() : this(null) { }

        // main app model
        public MainWindowViewModel(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;

            CachedSaveGame.SaveFileLoadStart += CachedSaveGame_SaveFileLoadStart;
            CachedSaveGame.SaveFileLoadEnd += CachedSaveGame_SaveFileLoadEnd;
            CachedSaveGame.SaveFileLoadError += CachedSaveGame_SaveFileLoadError;

            settings = Storage.LoadAppSettings();

            // remove manually-added locations which no longer exist
            var manualLocs = new List<string>(settings.ExtraSaveLocations);
            foreach (var loc in manualLocs)
            {
                if (!Directory.Exists(loc))
                {
                    var asSave = new SaveGame(loc);
                    Storage.ClearForSave(asSave);
                    manualLocs.Remove(loc);
                }
            }

            Storage.SaveAppSettings(settings);

            SaveSelection = new SaveSelectorViewModel(SavesLocation.AllLocal, settings.ExtraSaveLocations.Select(saveFolder => new SaveGame(saveFolder)));
            SolverControls = new SolverControlsViewModel();
            PalTargetList = new PalTargetListViewModel();

            targetsBySaveFile = SaveSelection.SavesLocations
                .SelectMany(l => l.SaveGames)
                .Where(vm => !vm.IsAddManualOption)
                .Select(sgvm => sgvm.Value)
                .ToDictionary(
                    sg => sg,
                    sg =>
                    {
                        var saveTargetsLocation = Storage.SaveFileDataPath(sg);
                        var targetsFile = Path.Join(saveTargetsLocation, "pal-targets.json");
                        if (File.Exists(targetsFile))
                        {
                            var originalCachedSave = Storage.LoadSaveFromCache(sg, db);
                            if (originalCachedSave == null)
                            {
                                logger.Warning("pal target list for {saveId} was detected but there was no cached data, which is required for loading the target list. resetting target list for this save", CachedSaveGame.IdentifierFor(sg));
                                File.Delete(targetsFile);
                                return new PalTargetListViewModel();
                            }

                            var converter = new PalTargetListViewModelConverter(db, new GameSettings(), originalCachedSave);
#if HANDLE_ERRORS
                            try
                            {
#endif
                                return JsonConvert.DeserializeObject<PalTargetListViewModel>(File.ReadAllText(targetsFile), converter);

#if HANDLE_ERRORS
                            }
                            catch (Exception ex)
                            {
                                logger.Warning(ex, "an error occurred loading targets list for {saveId}, deleting the old file and resetting", CachedSaveGame.IdentifierFor(sg));
                                File.Delete(targetsFile);
                                return new PalTargetListViewModel();
                            }
#endif
                        }
                        else
                        {
                            return new PalTargetListViewModel();
                        }
                    }
                );

            SaveSelection.PropertyChanged += SaveSelection_PropertyChanged;
            SaveSelection.NewCustomSaveSelected += SaveSelection_CustomSaveAdded;

            UpdateFromSaveProperties();
        }

        private void SaveSelection_CustomSaveAdded(ManualSavesLocationViewModel manualSaves, SaveGame save)
        {
            targetsBySaveFile.Add(save, new PalTargetListViewModel());

            var saveVm = manualSaves.Add(save);
            SaveSelection.SelectedGame = saveVm;

            settings.ExtraSaveLocations.Add(save.BasePath);
            Storage.SaveAppSettings(settings);
        }

        private void CachedSaveGame_SaveFileLoadStart(SaveGame obj)
        {
            if (loadingSaveModal == null)
            {
                loadingSaveModal = new LoadingSaveFileModal();
                loadingSaveModal.Owner = Application.Current.MainWindow;
                loadingSaveModal.DataContext = "Save file was not yet cached or cache is outdated, reading content...";
                loadingSaveModal.Show();
                AllowUIToUpdate();
            }
        }

        private void CachedSaveGame_SaveFileLoadEnd(SaveGame obj)
        {
            if (loadingSaveModal != null)
            {
                loadingSaveModal.Close();
                loadingSaveModal = null;
                AllowUIToUpdate();
            }
        }

        private void CachedSaveGame_SaveFileLoadError(SaveGame obj, Exception ex)
        {
            if (loadingSaveModal != null)
            {
                loadingSaveModal.Close();
                loadingSaveModal = null;
            }

            logger.Error(ex, "error when parsing save file for {saveId}", CachedSaveGame.IdentifierFor(obj));
            MessageBox.Show("An error occurred when loading the save file");
        }

        private void UpdateFromSaveProperties()
        {
            if (PalTargetList != null) PalTargetList.PropertyChanged -= PalTargetList_PropertyChanged;
            if (GameSettings != null) GameSettings.PropertyChanged -= GameSettings_PropertyChanged;

            if (SaveSelection.SelectedGame?.Value == null)
            {
                PalTargetList = null;
                PalTarget = null;
                GameSettings = null;
            }
            else
            {
                CrashSupport.ReferencedSave(SaveSelection.SelectedGame.Value);

                PalTargetList = targetsBySaveFile[SaveSelection.SelectedGame.Value];
                PalTargetList.PropertyChanged += PalTargetList_PropertyChanged;

                GameSettings = GameSettingsViewModel.Load(SaveSelection.SelectedGame.Value);
                GameSettings.PropertyChanged += GameSettings_PropertyChanged;
            }

            UpdatePalTarget();
        }

        private void UpdatePalTarget()
        {
            if (PalTargetList?.SelectedTarget != null)
                PalTarget = new PalTargetViewModel(PalTargetList.SelectedTarget);
            else
                PalTarget = null;
        }

        private void GameSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var saveGame = SaveSelection.SelectedGame?.Value;
            if (saveGame != null)
            {
                var settings = sender as GameSettingsViewModel;
                settings.Save(saveGame);
            }
        }

        private void SaveSelection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaveSelection.SelectedGame))
            {
                UpdateFromSaveProperties();
            }
        }

        private void PalTargetList_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalTargetList.SelectedTarget))
            {
                UpdatePalTarget();
            }
        }

        public void RunSolver()
        {
            var currentSpec = PalTarget.CurrentPalSpecifier.ModelObject;
            if (currentSpec == null) return;

            var cachedData = SaveSelection.SelectedGame.CachedValue;
            if (cachedData == null) return;

            var solver = SolverControls.ConfiguredSolver(GameSettings.ModelObject, cachedData.OwnedPals);
            solver.SolverStateUpdated += Solver_SolverStateUpdated;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsEditable = false);

                    solverTokenSource = new CancellationTokenSource();
                    var results = solver.SolveFor(currentSpec, solverTokenSource.Token);

                    dispatcher.Invoke(() =>
                    {
                        if (!solverTokenSource.IsCancellationRequested)
                        {
                            PalTarget.CurrentPalSpecifier.CurrentResults = new BreedingResultListViewModel() { Results = results.Select(r => new BreedingResultViewModel(cachedData, r)).ToList() };
                            if (PalTarget.InitialPalSpecifier == null)
                            {
                                PalTargetList.Add(PalTarget.CurrentPalSpecifier);
                                PalTargetList.SelectedTarget = PalTarget.CurrentPalSpecifier;
                            }
                            else
                            {
                                var updatedSpec = PalTarget.CurrentPalSpecifier;
                                PalTargetList.Replace(PalTarget.InitialPalSpecifier, updatedSpec);
                                PalTargetList.SelectedTarget = updatedSpec;
                            }

                            var outputFolder = Storage.SaveFileDataPath(SaveSelection.SelectedGame.Value);
                            if (!Directory.Exists(outputFolder))
                                Directory.CreateDirectory(outputFolder);

                            var outputFile = Path.Join(outputFolder, "pal-targets.json");
                            var converter = new PalTargetListViewModelConverter(db, new GameSettings(), SaveSelection.SelectedGame.CachedValue);
                            File.WriteAllText(outputFile, JsonConvert.SerializeObject(PalTargetList, converter));
                        }

                        solverTokenSource = null;
                        IsEditable = true;
                    });
                }
                catch (Exception e)
                {
                    dispatcher.BeginInvoke(() =>
                    {
                        // re-throw on UI thread so the app crashes (instead of hangs) with proper error handling
                        throw new Exception("Unhandled error during solver operation", e);
                    });
                }
            });
        }

        public void CancelSolver()
        {
            if (solverTokenSource != null)
            {
                solverTokenSource.Cancel();
            }
        }

        private void Solver_SolverStateUpdated(SolverStatus obj)
        {
            dispatcher.BeginInvoke(() =>
            {
                var numTotalSteps = (double)(1 + obj.TargetSteps * 2);
                int overallStep = 0;
                switch (obj.CurrentPhase)
                {
                    case SolverPhase.Initializing:
                        SolverStatusMsg = "Initializing";
                        overallStep = 0;
                        break;

                    case SolverPhase.Breeding:
                        SolverStatusMsg = $"Breeding step {obj.CurrentStepIndex + 1}, calculating child pals and probabilities";
                        overallStep = 1 + obj.CurrentStepIndex * 2;
                        
                        break;

                    case SolverPhase.Simplifying:
                        SolverStatusMsg = $"Breeding step {obj.CurrentStepIndex + 1}, simplifying results";
                        overallStep = 1 + obj.CurrentStepIndex * 2 + 1;
                        break;

                    case SolverPhase.Finished:
                        SolverStatusMsg = "Finished";
                        overallStep = (int)numTotalSteps;
                        break;
                }

                SolverProgress = 100 * overallStep / numTotalSteps;
            });
        }

        [ObservableProperty]
        private SaveSelectorViewModel saveSelection;
        [ObservableProperty]
        private GameSettingsViewModel gameSettings;
        [ObservableProperty]
        private SolverControlsViewModel solverControls;
        [ObservableProperty]
        private PalTargetListViewModel palTargetList;

        [ObservableProperty]
        private PalTargetViewModel palTarget;

        [ObservableProperty]
        private double solverProgress;

        [ObservableProperty]
        private string solverStatusMsg;

        private bool isEditable = true;
        public bool IsEditable
        {
            get => isEditable;
            set
            {
                if (SetProperty(ref isEditable, value))
                {
                    OnPropertyChanged(nameof(ProgressBarVisibility));
                    SolverControls.CanRunSolver = value;
                }
            }
        }

        public Visibility ProgressBarVisibility => IsEditable ? Visibility.Collapsed : Visibility.Visible;

        public PalDB DB => db;
    }
}
