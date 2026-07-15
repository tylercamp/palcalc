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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI.ViewModel
{
    internal class MainWindowViewModelLoadingProgress
    {
        public int LoadedSaves { get; set; }
        public int TotalSaves { get; set; }

        public double ProgressPercent => 100 * (TotalSaves > 0 ? (double)LoadedSaves / TotalSaves : 1);
    }

    

    internal partial class SolverPageViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<SolverPageViewModel>();
        private static PalDB db = PalDB.LoadEmbedded();
        private Dispatcher dispatcher;
        private AppSettings settings;
        private PassiveSkillsPresetCollectionViewModel passivePresets;
        private IRelayCommand<PalSpecifierViewModel> deletePalTargetCommand;

        public ICommand RunSolverCommand { get; }
        public ICommand PauseSolverCommand { get; }
        public ICommand ResumeSolverCommand { get; }
        public ICommand CancelSolverCommand { get; }

        public SaveGameViewModel2 OpenedSave { get; }

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

        public List<TranslationLocaleViewModel> Locales { get; } =
            Enum
                .GetValues<TranslationLocale>()
                .Select(l => new TranslationLocaleViewModel(l))
                .ToList();

        public SolverPageViewModel() : this(null, null, null) { }

        // main app model
        public SolverPageViewModel(Dispatcher dispatcher, SaveGameViewModel2 selectedSave, PalTargetListViewModel targets)
        {
            this.dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
            OpenedSave = selectedSave;

            settings = AppSettings.Current;
            settings.SolverSettings ??= new SerializableSolverSettings();
            passivePresets = new PassiveSkillsPresetCollectionViewModel(settings.PassiveSkillsPresets);

            

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

            PauseSolverCommand = new RelayCommand(PauseSolver);
            ResumeSolverCommand = new RelayCommand(ResumeSolver);
            CancelSolverCommand = new RelayCommand(CancelSolver);

            RunSolverCommand = new RelayCommand(RunSolver);

            dispatcher.Invoke(() =>
            {
                // (needed for XAML designer view)
                if (App.Current.MainWindow != null)
                {
                    App.Current.Exit += (o, e) =>
                    {
                        foreach (var target in SolverQueue.QueuedItems)
                            target.LatestJob.Cancel();
                    };

                    App.Current.MainWindow.Closing += (o, e) =>
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
                }
            });

            SolverControls = new SolverControlsViewModel(
                runSolverCommand: RunSolverCommand,
                cancelSolverCommand: CancelSolverCommand,
                pauseSolverCommand: PauseSolverCommand,
                resumeSolverCommand: ResumeSolverCommand
            );
            SolverControls.CopyFrom(settings.SolverSettings);
            SolverControls.PropertyChanged += (s, e) =>
            {
                settings.SolverSettings = SolverControls.AsModel;
                Storage.SaveAppSettings(settings);
            };

            PalTargetList = targets;
            
            // TODO - would prefer to have the delete command managed by the target list, rather than having
            //        to manually assign the command for each specifier VM
            deletePalTargetCommand = new RelayCommand<PalSpecifierViewModel>(OnDeletePalSpecifier);
            foreach (var target in targets.Targets.Where(t => !t.IsReadOnly))
                target.DeleteCommand = deletePalTargetCommand;

            targets.OrderChanged += SaveTargetList;

            Storage.SaveReloaded += Storage_SaveReloaded;

            dispatcher.Invoke(UpdateFromSaveProperties);

            passivePresets.PresetSelected += selectedPreset =>
            {
                var spec = PalTarget?.CurrentPalSpecifier;
                if (spec != null) selectedPreset.ApplyTo(spec);
            };

            SolverQueue.SelectItemCommand = new RelayCommand<PalSpecifierViewModel>(vm => PalTargetList.SelectedTarget = PalTargetList.Targets.FirstOrDefault(t => t.LatestJob == vm.LatestJob));

            CheckForUpdates();
        }

        private void Storage_SaveReloaded(ISaveGame save)
        {
            if (OpenedSave?.Value == save)
                UpdateFromSaveProperties();
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
                PalTarget = new PalTargetViewModel(OpenedSave, PalTargetList.SelectedTarget, passivePresets);
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
            var converter = new PalTargetListViewModelConverter(db, new GameSettings(), OpenedSave.CachedValue, list.Targets.Where(t => !t.IsReadOnly).ToDictionary(t => t.Id));
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
                SolverControls.ConfiguredSolver(originalGameSettings, PalTarget.AvailablePals.ToList()),
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
                                    dispatcher?.BeginInvoke(() => UpdatesMessageVisibility = Visibility.Visible);
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
    }
}
