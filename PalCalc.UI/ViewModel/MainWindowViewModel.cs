using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
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
        private PassiveSkillsPresetCollectionViewModel passivePresets;
        private IRelayCommand<PalSpecifierViewModel> deletePalTargetCommand;

        public List<TranslationLocaleViewModel> Locales { get; } =
            Enum
                .GetValues<TranslationLocale>()
                .Select(l => new TranslationLocaleViewModel(l))
                .ToList();

        public MainWindowViewModel() : this(null) { }

        // main app model
        public MainWindowViewModel(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

            CachedSaveGame.SaveFileLoadStart += CachedSaveGame_SaveFileLoadStart;
            CachedSaveGame.SaveFileLoadEnd += CachedSaveGame_SaveFileLoadEnd;
            CachedSaveGame.SaveFileLoadError += CachedSaveGame_SaveFileLoadError;

            AppSettings.Current = settings = Storage.LoadAppSettings();
            settings.SolverSettings ??= new SolverSettings();
            passivePresets = new PassiveSkillsPresetCollectionViewModel(settings.PassiveSkillsPresets);

            Translator.CurrentLocale = settings.Locale;

            Translator.LocaleUpdated += () =>
            {
                if (settings.Locale != Translator.CurrentLocale)
                {
                    settings.Locale = Translator.CurrentLocale;
                    Storage.SaveAppSettings(settings);
                }
            };

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

            var manualSaves = settings.ExtraSaveLocations.Select(saveFolder => new StandardSaveGame(saveFolder)).ToList();
            var fakeSaves = settings.FakeSaveNames.Select(FakeSaveGame.Create).ToList();
            SaveSelection = new SaveSelectorViewModel(availableSavesLocations, manualSaves.Concat(fakeSaves));

            targetsBySaveFile = SaveSelection.SavesLocations
                .SelectMany(l => l.SaveGames)
                .OfType<SaveGameViewModel>()
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
            SaveSelection.CustomSaveDelete += SaveSelection_CustomSaveDelete;

            if (settings.SelectedGameIdentifier != null) SaveSelection.TrySelectSaveGame(settings.SelectedGameIdentifier);
            
            // TODO - would prefer to have the delete command managed by the target list, rather than having
            //        to manually assign the command for each specifier VM
            deletePalTargetCommand = new RelayCommand<PalSpecifierViewModel>(OnDeletePalSpecifier);
            foreach (var target in targetsBySaveFile.Values.SelectMany(l => l.Targets).Where(t => !t.IsReadOnly))
                target.DeleteCommand = deletePalTargetCommand;

            Storage.SaveReloaded += Storage_SaveReloaded;

            dispatcher.BeginInvoke(UpdateFromSaveProperties, DispatcherPriority.Background);

            passivePresets.PresetSelected += selectedPreset =>
            {
                var spec = PalTarget?.CurrentPalSpecifier;
                if (spec != null) selectedPreset.ApplyTo(spec);
            };

            CheckForUpdates();
        }

        private void Storage_SaveReloaded(ISaveGame save)
        {
            if (SaveSelection?.SelectedFullGame?.Value == save)
                UpdateFromSaveProperties();
        }

        private void OnDeletePalSpecifier(PalSpecifierViewModel spec)
        {
            if (spec == null) return;

            if (SaveSelection?.SelectedFullGame == null)
            {
                return;
            }

            var targetList = targetsBySaveFile[SaveSelection.SelectedFullGame.Value];

            if (!targetList.Targets.Contains(spec))
            {
                return;
            }

            var title = LocalizationCodes.LC_DELETE_PAL_TARGET_TITLE.Bind().Value;
            var msg = LocalizationCodes.LC_DELETE_PAL_TARGET_MSG.Bind(spec.TargetPal.Name).Value;

            if (MessageBox.Show(App.Current.MainWindow, msg, title, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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

            if (save is VirtualSaveGame)
            {
                settings.FakeSaveNames.Add(FakeSaveGame.GetLabel(save));
            }
            else
            {
                settings.ExtraSaveLocations.Add(save.BasePath);
            }

            Storage.SaveAppSettings(settings);
        }

        private void SaveSelection_CustomSaveDelete(ManualSavesLocationViewModel manualSaves, ISaveGame saveGame)
        {
            var saveVm = manualSaves.SaveGames.OfType<SaveGameViewModel>().First(sg => sg.Value == saveGame);

            var confirmation = MessageBox.Show(
                App.ActiveWindow,
                LocalizationCodes.LC_REMOVE_SAVE_DESCRIPTION.Bind(saveVm.Label).Value,
                LocalizationCodes.LC_REMOVE_SAVE_TITLE.Bind().Value,
                MessageBoxButton.YesNo
            );

            if (confirmation == MessageBoxResult.Yes)
            {
                var toClose = App.Current.Windows
                    .OfType<SaveInspectorWindow>()
                    .Where(w => (w.DataContext as SaveInspectorWindowViewModel)?.DisplayedSave?.Value == saveGame)
                    .ToList();

                foreach (var window in toClose)
                {
                    foreach (var child in window.OwnedWindows.OfType<Window>().ToList())
                        child.Close();

                    window.Close();
                }

                manualSaves.Remove(saveGame);

                if (saveGame is VirtualSaveGame)
                {
                    settings.FakeSaveNames.Remove(FakeSaveGame.GetLabel(saveGame));
                    saveVm.Customizations.Dispose();
                }
                else
                {
                    settings.ExtraSaveLocations.Remove(saveGame.BasePath);
                }

                Storage.RemoveSave(saveGame);
                saveGame.Dispose();

                Storage.SaveAppSettings(settings);
            }
        }

        private void CachedSaveGame_SaveFileLoadStart(ISaveGame obj)
        {
            if (loadingSaveModal == null)
            {
                loadingSaveModal = new LoadingSaveFileModal();
                loadingSaveModal.Owner = Application.Current.MainWindow;
                loadingSaveModal.DataContext = LocalizationCodes.LC_SAVE_FILE_RELOADING.Bind();
                loadingSaveModal.ShowSync();
            }
        }

        private void CachedSaveGame_SaveFileLoadEnd(ISaveGame obj, CachedSaveGame loaded)
        {
            if (loadingSaveModal != null)
            {
                loadingSaveModal.Close();
                loadingSaveModal = null;

                if (loaded != null && targetsBySaveFile.ContainsKey(obj))
                    targetsBySaveFile[obj].RefreshWith(loaded);
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

            var crashsupport = CrashSupport.PrepareSupportFile(specificSave: obj);
            MessageBox.Show(LocalizationCodes.LC_ERROR_SAVE_LOAD_FAILED.Bind(crashsupport).Value);

            SaveSelection.SelectedGame = null;
        }

        private void UpdateFromSaveProperties()
        {
            if (PalTargetList != null) PalTargetList.PropertyChanged -= PalTargetList_PropertyChanged;
            if (GameSettings != null) GameSettings.PropertyChanged -= GameSettings_PropertyChanged;

            if (SaveSelection.SelectedFullGame?.Value == null)
            {
                PalTargetList = null;
                PalTarget = null;
                GameSettings = null;
            }
            else
            {
                CrashSupport.ReferencedSave(SaveSelection.SelectedFullGame.Value);

                settings.SelectedGameIdentifier = CachedSaveGame.IdentifierFor(SaveSelection.SelectedFullGame.Value);
                Storage.SaveAppSettings(settings);

                PalTargetList = targetsBySaveFile[SaveSelection.SelectedFullGame.Value];
                PalTargetList.PropertyChanged += PalTargetList_PropertyChanged;

                GameSettings = GameSettingsViewModel.Load(SaveSelection.SelectedFullGame.Value);
                GameSettings.PropertyChanged += GameSettings_PropertyChanged;
            }

            UpdatePalTarget();
            UpdateSolverControls();
        }

        private void UpdatePalTarget()
        {
            if (PalTargetList?.SelectedTarget != null && SaveSelection.SelectedFullGame?.CachedValue != null)
            {
                PalTarget = new PalTargetViewModel(SaveSelection.SelectedFullGame.CachedValue, PalTargetList.SelectedTarget, passivePresets);
                passivePresets.ActivePalTarget = PalTarget;
            }
            else
                PalTarget = null;
        }

        private void GameSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var saveGame = SaveSelection.SelectedFullGame?.Value;
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
            var outputFolder = Storage.SaveFileDataPath(SaveSelection.SelectedFullGame.Value);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var outputFile = Path.Join(outputFolder, "pal-targets.json");
            var converter = new PalTargetListViewModelConverter(db, new GameSettings(), SaveSelection.SelectedFullGame.CachedValue);
            File.WriteAllText(outputFile, JsonConvert.SerializeObject(list, converter));
        }

        public void RunSolver()
        {
            var currentSpec = PalTarget?.CurrentPalSpecifier?.ModelObject;
            if (currentSpec == null) return;

            var selectedGame = SaveSelection.SelectedFullGame;
            var cachedData = selectedGame.CachedValue;
            if (cachedData == null) return;

            var inputPals = PalTarget.PalSource.SelectedSource.Filter(cachedData);
            if (!PalTarget.CurrentPalSpecifier.IncludeBasePals)
                inputPals = inputPals.Where(p => p.Location.Type != LocationType.Base && p.Location.Type != LocationType.ViewingCage);

            if (PalTarget.CurrentPalSpecifier.IncludeCustomPals)
                inputPals = inputPals.Concat(selectedGame.Customizations.ModelObject.CustomContainers.SelectMany(c => c.Contents));

            var solver = SolverControls.ConfiguredSolver(GameSettings.ModelObject, inputPals.ToList());
            solver.SolverStateUpdated += Solver_SolverStateUpdated;

            var solverThread = new Thread(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsEditable = false);

                    solverTokenSource = new CancellationTokenSource();
                    var results = solver.SolveFor(currentSpec, solverTokenSource.Token);

                    // general simplification pass, get the best result for each potentially
                    // interesting combination of result properties
                    var resultsTable = new PalPropertyGrouping(PalProperty.Combine(
                        PalProperty.EffectivePassives,
                        PalProperty.NumBreedingSteps,
                        p => p.AllReferences().Select(r => r.Location.GetType()).Distinct().SetHash()
                    ));
                    resultsTable.AddRange(results);
                    resultsTable.FilterAll(PruningRulesBuilder.Default, solverTokenSource.Token);

                    // final simplification pass, ignore any results which are over 2x the effort of the fastest option
                    resultsTable = resultsTable.BuildNew(PalProperty.Combine(
                        PalProperty.EffectivePassives
                    ));
                    resultsTable.FilterAll(g =>
                    {
                        // (though if "the fastest option" is just a pal we already own with 0 effort, don't count that)
                        var nonZero = g.Where(r => r.BreedingEffort > TimeSpan.Zero).ToList();
                        if (nonZero.Count != 0)
                        {
                            var fastest = g.Where(r => r.BreedingEffort > TimeSpan.Zero).Min(r => r.BreedingEffort);
                            return g.Where(r => r.BreedingEffort <= fastest * 2);
                        }
                        else
                        {
                            return g.Take(1);
                        }
                    });

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

            solverThread.Priority = ThreadPriority.BelowNormal;
            solverThread.Start();
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
            string FormatNum(int num) => num.ToString("#,##");

            dispatcher.BeginInvoke(() =>
            {
                var numTotalSteps = (double)(1 + obj.TargetSteps);
                int overallStep = 0;
                switch (obj.CurrentPhase)
                {
                    case SolverPhase.Initializing:
                        solverStopwatch = Stopwatch.StartNew();
                        SolverStatusMsg = LocalizationCodes.LV_SOLVER_STATUS_INITIALIZING.Bind();
                        overallStep = 0;

                        StepProgress = 0;
                        StepStatusMsg = null;
                        break;

                    case SolverPhase.Breeding:
                        SolverStatusMsg = LocalizationCodes.LC_SOLVER_STATUS_BREEDING.Bind(
                            new
                            {
                                StepNum = obj.CurrentStepIndex + 1,
                                WorkSize = FormatNum(obj.CurrentWorkSize),
                            }
                        );
                        overallStep = 1 + obj.CurrentStepIndex;

                        StepProgress = 100 * (obj.WorkProcessedCount / (double)obj.CurrentWorkSize);
                        StepStatusMsg = LocalizationCodes.LC_SOLVER_STEP_STATUS_BREEDING.Bind(
                            new { NumProcessed = FormatNum(obj.WorkProcessedCount), WorkSize = FormatNum(obj.CurrentWorkSize) }
                        );
                        break;

                    case SolverPhase.Finished:
                        if (obj.Canceled)
                        {
                            SolverStatusMsg = null;
                        }
                        else
                        {
                            SolverStatusMsg = LocalizationCodes.LC_SOLVER_STATUS_FINISHED.Bind(solverStopwatch.Elapsed.TimeSpanSecondsStr());
                            overallStep = (int)numTotalSteps;
                            StepProgress = 100;
                            StepStatusMsg = LocalizationCodes.LC_SOLVER_STEP_STATUS_DONE.Bind(FormatNum(obj.TotalWorkProcessedCount));
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

        [ObservableProperty]
        private double stepProgress;

        [NotifyPropertyChangedFor(nameof(ProgressBarVisibility))]
        [ObservableProperty]
        private ILocalizedText solverStatusMsg;

        [ObservableProperty]
        private ILocalizedText stepStatusMsg;

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

        public Visibility ProgressBarVisibility => SolverStatusMsg == null ? Visibility.Collapsed : Visibility.Visible;

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
