using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel.Mapped;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<MainWindowViewModel>();
        private static PalDB db = PalDB.LoadEmbedded();
        private Dictionary<ISaveGame, PalTargetListViewModel> targetsBySaveFile;
        private LoadingSaveFileModal loadingSaveModal = null;
        private Dispatcher dispatcher;
        private CancellationTokenSource solverTokenSource;
        private AppSettings settings;
        private IRelayCommand<PalSpecifierViewModel> deletePalTargetCommand;

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
            settings.SolverSettings ??= new SolverSettings();

            // remove manually-added locations which no longer exist
            var manualLocs = settings.ExtraSaveLocations
                .Where(loc =>
                {
                    var exists = Directory.Exists(loc);
                    if (!exists)
                    {
                        var asSave = new StandardSaveGame(loc);
                        Storage.ClearForSave(asSave);
                    }
                    return exists;
                })
                .ToList();

            Storage.SaveAppSettings(settings);

            SolverControls = SolverControlsViewModel.FromModel(settings.SolverSettings);
            SolverControls.PropertyChanged += (s, e) =>
            {
                settings.SolverSettings = SolverControls.AsModel;
                Storage.SaveAppSettings(settings);
            };

            PalTargetList = new PalTargetListViewModel();

            var availableSavesLocations = new List<ISavesLocation>();
            availableSavesLocations.AddRange(DirectSavesLocation.AllLocal);

            var xboxLocations = XboxSavesLocation.FindAll();
            if (xboxLocations.Count > 0) availableSavesLocations.AddRange(xboxLocations);
            else
            {
                // add a placeholder so the user can (optionally) see the explanation why no saves are available (game isn't installed/synced via xbox app)
                availableSavesLocations.Add(new XboxSavesLocation());
            }
            
            SaveSelection = new SaveSelectorViewModel(availableSavesLocations, settings.ExtraSaveLocations.Select(saveFolder => new StandardSaveGame(saveFolder)));

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

                            var converter = new PalTargetListViewModelConverter(db, GameSettingsViewModel.Load(sg).ModelObject, originalCachedSave);
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

            if (settings.SelectedGameIdentifier != null) SaveSelection.TrySelectSaveGame(settings.SelectedGameIdentifier);

            
            // TODO - would prefer to have the delete command managed by the target list, rather than having
            //        to manually assign the command for each specifier VM
            deletePalTargetCommand = new RelayCommand<PalSpecifierViewModel>(OnDeletePalSpecifier);
            foreach (var target in targetsBySaveFile.Values.SelectMany(l => l.Targets).Where(t => !t.IsReadOnly))
                target.DeleteCommand = deletePalTargetCommand;


            dispatcher.BeginInvoke(UpdateFromSaveProperties, DispatcherPriority.Background);

            CheckForUpdates();
        }

        private void OnDeletePalSpecifier(PalSpecifierViewModel spec)
        {
            if (spec == null) return;

            if (SaveSelection?.SelectedGame == null)
            {
                return;
            }

            var targetList = targetsBySaveFile[SaveSelection.SelectedGame.Value];

            if (!targetList.Targets.Contains(spec))
            {
                return;
            }

            if (MessageBox.Show(App.Current.MainWindow, $"Delete this entry for {spec.TargetPal.Name}?", "Delete Pal Target", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                targetList.Remove(spec);
                SaveTargetList(targetList);

                UpdatePalTarget();
            }
        }

        private void SaveSelection_CustomSaveAdded(ManualSavesLocationViewModel manualSaves, ISaveGame save)
        {
            if (Storage.LoadSave(save, db) == null)
            {
                SaveSelection.SelectedGame = null;
                return;
            }

            targetsBySaveFile.Add(save, new PalTargetListViewModel());

            var saveVm = manualSaves.Add(save);
            SaveSelection.SelectedGame = saveVm;

            settings.ExtraSaveLocations.Add(save.BasePath);
            Storage.SaveAppSettings(settings);
        }

        private void CachedSaveGame_SaveFileLoadStart(ISaveGame obj)
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

        private void CachedSaveGame_SaveFileLoadEnd(ISaveGame obj)
        {
            if (loadingSaveModal != null)
            {
                loadingSaveModal.Close();
                loadingSaveModal = null;
                AllowUIToUpdate();
            }
        }

        private void CachedSaveGame_SaveFileLoadError(ISaveGame obj, Exception ex)
        {
            if (loadingSaveModal != null)
            {
                loadingSaveModal.Close();
                loadingSaveModal = null;
            }

            logger.Error(ex, "error when parsing save file for {saveId}", CachedSaveGame.IdentifierFor(obj));

            var crashsupport = CrashSupport.PrepareSupportFile(obj);
            MessageBox.Show($"An error occurred when loading the save file.\n\nPlease find the generated ZIP file to send with any support questions:\n\n{crashsupport}");

            SaveSelection.SelectedGame = null;
        }

        private void UpdateFromSaveProperties()
        {
            if (!Application.Current.MainWindow.IsVisible)
            {
                // fix error due to trying to show the "loading save file" window when the main window hasn't been presented yet
                dispatcher.BeginInvoke(() => UpdateFromSaveProperties(), DispatcherPriority.ApplicationIdle);
                return;
            }

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

                settings.SelectedGameIdentifier = CachedSaveGame.IdentifierFor(SaveSelection.SelectedGame.Value);
                Storage.SaveAppSettings(settings);

                PalTargetList = targetsBySaveFile[SaveSelection.SelectedGame.Value];
                PalTargetList.PropertyChanged += PalTargetList_PropertyChanged;

                GameSettings = GameSettingsViewModel.Load(SaveSelection.SelectedGame.Value);
                GameSettings.PropertyChanged += GameSettings_PropertyChanged;
            }

            UpdatePalTarget();
            UpdateSolverControls();
        }

        private void UpdatePalTarget()
        {
            if (PalTargetList?.SelectedTarget != null && SaveSelection.SelectedGame?.CachedValue != null)
            {
                PalTarget = new PalTargetViewModel(SaveSelection.SelectedGame.CachedValue, PalTargetList.SelectedTarget);
            }
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

        private void SaveTargetList(PalTargetListViewModel list)
        {
            var outputFolder = Storage.SaveFileDataPath(SaveSelection.SelectedGame.Value);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var outputFile = Path.Join(outputFolder, "pal-targets.json");
            var converter = new PalTargetListViewModelConverter(db, new GameSettings(), SaveSelection.SelectedGame.CachedValue);
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(list, converter));
        }

        public void RunSolver()
        {
            var currentSpec = PalTarget?.CurrentPalSpecifier?.ModelObject;
            if (currentSpec == null) return;

            var cachedData = SaveSelection.SelectedGame.CachedValue;
            if (cachedData == null) return;

            var inputPals = PalTarget.PalSource.SelectedSource.Filter(cachedData);
            if (!PalTarget.CurrentPalSpecifier.IncludeBasePals)
                inputPals = inputPals.Where(p => p.Location.Type != LocationType.Base);

            var solver = SolverControls.ConfiguredSolver(GameSettings.ModelObject, inputPals.ToList());
            solver.SolverStateUpdated += Solver_SolverStateUpdated;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsEditable = false);

                    solverTokenSource = new CancellationTokenSource();
                    var results = solver.SolveFor(currentSpec, solverTokenSource.Token);

                    var resultsTable = new PalPropertyGrouping(PalProperty.Combine(
                        PalProperty.EffectiveTraits,
                        p => p.AllReferences().Select(r => r.Location.GetType()).Distinct().SetHash()
                    ));
                    resultsTable.AddRange(results);
                    resultsTable.FilterAll(PruningRulesBuilder.Default, solverTokenSource.Token);

                    results = resultsTable.All.ToList();

                    dispatcher.Invoke(() =>
                    {
                        if (!solverTokenSource.IsCancellationRequested)
                        {
                            PalTarget.CurrentPalSpecifier.CurrentResults = new BreedingResultListViewModel() { Results = results.Select(r => new BreedingResultViewModel(cachedData, r)).ToList() };
                            if (PalTarget.InitialPalSpecifier == null)
                            {
                                PalTarget.CurrentPalSpecifier.DeleteCommand = deletePalTargetCommand;
                                PalTargetList.Add(PalTarget.CurrentPalSpecifier);
                                PalTargetList.SelectedTarget = PalTarget.CurrentPalSpecifier;
                            }
                            else
                            {
                                var updatedSpec = PalTarget.CurrentPalSpecifier;
                                PalTargetList.Replace(PalTarget.InitialPalSpecifier, updatedSpec);
                                PalTargetList.SelectedTarget = updatedSpec;
                            }

                            SaveTargetList(PalTargetList);

                            UpdatePalTarget();
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

        private Stopwatch solverStopwatch = null;
        private void Solver_SolverStateUpdated(SolverStatus obj)
        {
            dispatcher.BeginInvoke(() =>
            {
                var numTotalSteps = (double)(1 + obj.TargetSteps);
                int overallStep = 0;
                switch (obj.CurrentPhase)
                {
                    case SolverPhase.Initializing:
                        solverStopwatch = Stopwatch.StartNew();
                        SolverStatusMsg = "Initializing";
                        overallStep = 0;
                        break;

                    case SolverPhase.Breeding:
                        SolverStatusMsg = $"Breeding step {obj.CurrentStepIndex + 1}, calculating child pals and probabilities of {obj.WorkSize.ToString("#,##")} pairs";
                        overallStep = 1 + obj.CurrentStepIndex;
                        break;

                    case SolverPhase.Finished:
                        if (obj.Canceled)
                        {
                            SolverStatusMsg = null;
                        }
                        else
                        {
                            SolverStatusMsg = $"Finished (took {solverStopwatch.Elapsed.TimeSpanSecondsStr()})";
                            overallStep = (int)numTotalSteps;
                        }
                        break;
                }

                SolverProgress = 100 * overallStep / numTotalSteps;
            }).Wait();
        }

        [ObservableProperty]
        private SaveSelectorViewModel saveSelection;
        [ObservableProperty]
        private GameSettingsViewModel gameSettings;
        [ObservableProperty]
        private SolverControlsViewModel solverControls;
        [ObservableProperty]
        private PalTargetListViewModel palTargetList;

        private void UpdateSolverControls()
        {
            SolverControls.CanRunSolver = IsEditable && PalTarget != null && PalTarget.IsValid;
            SolverControls.CanEditSettings = IsEditable;
            SolverControls.CanCancelSolver = !IsEditable;
        }

        private PalTargetViewModel palTarget;
        public PalTargetViewModel PalTarget
        {
            get => palTarget;
            set
            {
                var oldValue = PalTarget;
                if (SetProperty(ref palTarget, value))
                {
                    if (oldValue != null) oldValue.PropertyChanged -= PalTarget_PropertyChanged;

                    if (value != null) value.PropertyChanged += PalTarget_PropertyChanged;

                    UpdateSolverControls();
                }
            }
        }

        private void PalTarget_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalTarget.IsValid)) UpdateSolverControls();
        }

        [ObservableProperty]
        private double solverProgress;

        [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
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
                    UpdateSolverControls();
                }
            }
        }

        public Visibility ProgressBarVisibility => string.IsNullOrEmpty(SolverStatusMsg) ? Visibility.Collapsed : Visibility.Visible;

        [ObservableProperty]
        private Visibility updatesMessageVisibility = Visibility.Collapsed;

        private string VersionFromUrl(string url) => url.Split('/').Last();
        private string latestVersionUrl;

        private void CheckForUpdates()
        {
            Task.Run(async () =>
            {
                try
                {
                    var latestReleaseUrl = $"{App.RepositoryUrl}/releases/latest";
                    using (var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }))
                    using (var response = await client.GetAsync(latestReleaseUrl))
                    {
                        if (response.StatusCode == HttpStatusCode.Found)
                        {
                            var location = response.Headers?.Location;
                            if (location != null)
                            {
                                latestVersionUrl = location.AbsoluteUri;
                                var latestVersion = VersionFromUrl(latestVersionUrl);
                                if (latestVersion != App.Version)
                                {
                                    dispatcher.BeginInvoke(() => UpdatesMessageVisibility = Visibility.Visible);
                                }
                            }
                            else
                            {
                                logger.Warning("releases response did not include redirect URL, unable to determine latest version");
                            }
                        }
                        else
                        {
                            logger.Warning("did not receive FOUND status code, unable to determine latest version");
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Warning(e, "error fetching latest version");
                }
            });
        }

        public void TryDownloadLatestVersion()
        {
            Process.Start(new ProcessStartInfo { FileName = latestVersionUrl, UseShellExecute = true });
        }

        public PalDB DB => db;
    }
}
