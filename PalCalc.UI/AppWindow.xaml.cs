using AdonisUI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.SaveSelection;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.ApplicationModel.VoiceCommands;

namespace PalCalc.UI
{
    internal partial class AppWindowViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<AppWindowViewModel>();

        private AppSettings settings;
        private ISavesService savesService;
        private PalDB db;
        private Dispatcher dispatcher;

        // TODO - updates notification? translations dropdown?

        [ObservableProperty]
        private bool showToolbar = false;

        private AppToolbarViewModel toolbarVM;
        public AppToolbarViewModel ToolbarVM => toolbarVM;

        private bool checkedUpdates;
        private AppUpdatesViewModel appUpdates;

        public AppWindowViewModel(Dispatcher dispatcher)
        {
            AppSettings.Current = settings = Storage.LoadAppSettings();
            savesService = new AppSettingsSaveService(settings);
            appUpdates = new AppUpdatesViewModel();
            checkedUpdates = false;
            this.dispatcher = dispatcher;

            Translator.CurrentLocale = settings.Locale;

            Translator.LocaleUpdated += () =>
            {
                if (settings.Locale != Translator.CurrentLocale)
                {
                    settings.Locale = Translator.CurrentLocale;
                    Storage.SaveAppSettings(settings);
                }
            };

            toolbarVM = new AppToolbarViewModel(dispatcher, appUpdates);

            //CachedSaveGame.SaveFileLoadEnd += CachedSaveGame_SaveFileLoadEnd;
            //CachedSaveGame.SaveFileLoadError += CachedSaveGame_SaveFileLoadError;

            BeginNavigateSaveSelectionPage();

            dispatcher.BeginInvoke(() =>
            {
                Task.Run(() =>
                {
                    db = PalDB.LoadEmbedded();
                    PalBreedingDB.BeginLoadEmbedded(db);
                });
            }, DispatcherPriority.ContextIdle);
        }

        private bool CanBeginNavigateSaveSelectionPage() => Content is SolverPage;

        [RelayCommand(CanExecute = nameof(CanBeginNavigateSaveSelectionPage))]
        private void BeginNavigateSaveSelectionPage()
        {
            var loadingPage = new LoadingPage();
            Content = loadingPage;
            ShowToolbar = false;

            dispatcher.BeginInvoke(() =>
            {
                Task.Run(() =>
                {
                    try
                    {
                        var saves = SavesDetection.FindAll(settings, savesService);
                        App.Current.Dispatcher.BeginInvoke(() =>
                        {
                            NavigateSaveSelectionPage(saves);
                            ShowToolbar = true;

                            if (!checkedUpdates)
                            {
                                RunStartupUpdatesCheck();
                                checkedUpdates = true;
                            }
                        }, DispatcherPriority.ContextIdle);
                    }
                    catch (Exception e)
                    {
                        // (exceptions in Tasks are handled differently - re-send exceptions on UI Dispatcher so it gets handled like a normal error)
                        dispatcher.BeginInvoke(() =>
                        {
                            throw new Exception("An error occurred while detecting available saves", e);
                        });
                    }
                });
            }, DispatcherPriority.ContextIdle);
        }

        private void RunStartupUpdatesCheck()
        {
            Task.Run(async () =>
            {
                var newVersion = await appUpdates.FetchNewUpdateUrl();
                if (newVersion == null)
                    return;

                if (settings.SkippedAppVersion == newVersion.Version)
                    return;

                dispatcher.BeginInvoke(
                    () => appUpdates.PromptUpdateDownload(newVersion),
                    DispatcherPriority.ContextIdle
                );
            });
        }

        private void NavigateSaveSelectionPage(IEnumerable<SavesCollectionViewModel> collections)
        {
            var vm = new SaveSelectionOnboardingViewModel(
                savesCollections: collections,
                loadSaveCommand: new RelayCommand<SaveGameViewModel2>(NavigateSolverPage)
            );

            var page = new SaveSelectionOnboardingPage();
            if (settings.SelectedGameIdentifier != null)
            {
                vm.TrySelectSaveByIdentifier(settings.SelectedGameIdentifier);
            }

            page.DataContext = vm;

            Content = page;
        }

        private void NavigateSolverPage(SaveGameViewModel2 selectedSave)
        {
            settings.SelectedGameIdentifier = CachedSaveGame.IdentifierFor(selectedSave.Value);
            Storage.SaveAppSettings(settings);
            CrashSupport.ReferencedSave(selectedSave.Value);

            var parsedSave = CachedSaveGame.FromSaveGame(null /* TODO */, selectedSave.Value, db, GameSettingsViewModel.Load(selectedSave.Value).ModelObject);
            if (parsedSave == null)
                return;

            var saveOperations = new CommonSaveOperationsViewModel(BeginNavigateSaveSelectionPageCommand, selectedSave.Parent, selectedSave);
            var vm = new SolverPageViewModel(Dispatcher.CurrentDispatcher, saveOperations, selectedSave, LoadPalTargets(selectedSave));
            Content = new SolverPage(vm);
        }

        private PalTargetListViewModel LoadPalTargets(SaveGameViewModel2 sg)
        {
            var gameSettings = GameSettingsViewModel.Load(sg.Value).ModelObject;
            var originalCachedSave = Storage.LoadSaveFromCache(sg.Value, db);
            var dataPath = Storage.SaveFileDataPath(sg.Value);

            var targetsFolder = Storage.SaveFileTargetsDataPath(sg.Value);
            PalTargetListViewModel result = null;
            if (File.Exists(Path.Join(dataPath, "pal-targets.json")))
            {
                result = PCDebug.HandleErrors(
                    action: () =>
                    {
                        // old format where pal targets were all stored in a single file, split into multiple files instead
                        Directory.CreateDirectory(targetsFolder);

                        var vmEntryConverter = new PalSpecifierViewModelConverter(db, gameSettings, originalCachedSave);
                        var oldData = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Path.Join(dataPath, "pal-targets.json")));
                        var oldTargets = oldData["Targets"]?.ToObject<List<PalSpecifierViewModel>>(JsonSerializer.Create(new JsonSerializerSettings() { Converters = [vmEntryConverter] }));

                        foreach (var target in oldTargets)
                        {
                            File.WriteAllText(Path.Join(targetsFolder, $"{target.Id}.json"), JsonConvert.SerializeObject(target, vmEntryConverter));
                        }

                        var res = new PalTargetListViewModel(oldTargets);
                        File.WriteAllText(
                            Path.Join(dataPath, "pal-target-ids.json"),
                            JsonConvert.SerializeObject(res, new PalTargetListViewModelConverter(db, gameSettings, originalCachedSave, oldTargets.ToDictionary(t => t.Id)))
                        );

                        File.Delete(Path.Join(dataPath, "pal-targets.json"));
                        return res;
                    },
                    handleErr: (ex) =>
                    {
                        logger.Warning(ex, "an error occurred loading the old targets list for {saveId}, skipping", CachedSaveGame.IdentifierFor(sg.Value));
                        return new PalTargetListViewModel();
                    }
                );
            }
            else if (File.Exists(Path.Join(dataPath, "pal-target-ids.json")))
            {
                result = PCDebug.HandleErrors(
                    action: () =>
                    {
                        var targetFiles = Directory.Exists(targetsFolder) ? Directory.EnumerateFiles(targetsFolder) : [];

                        var entryConverter = new PalSpecifierViewModelConverter(db, gameSettings, originalCachedSave);
                        var targetEntries = targetFiles.Select<string, PalSpecifierViewModel>(f =>
                        {
                            return PCDebug.HandleErrors(
                                action: () => JsonConvert.DeserializeObject<PalSpecifierViewModel>(File.ReadAllText(f), entryConverter),
                                handleErr: (ex) =>
                                {
                                    logger.Warning(ex, "an error occurred loading target for {saveId} at {path}, skipping", CachedSaveGame.IdentifierFor(sg.Value), f);
                                    return null;
                                }
                            );
                        }).SkipNull().ToList();

                        var converter = new PalTargetListViewModelConverter(db, GameSettingsViewModel.Load(sg.Value).ModelObject, originalCachedSave, targetEntries.ToDictionary(e => e.Id));
                        return JsonConvert.DeserializeObject<PalTargetListViewModel>(File.ReadAllText(Path.Join(dataPath, "pal-target-ids.json")), [converter]);
                    },
                    handleErr: (ex) =>
                    {
                        logger.Warning(ex, "an error occurred loading targets list for {saveId}, skipping", CachedSaveGame.IdentifierFor(sg.Value));
                        return new PalTargetListViewModel();
                    }
                );
            }
            else
            {
                result = new PalTargetListViewModel();
            }

            return result;
        }

        //private void CachedSaveGame_SaveFileLoadEnd(ISaveGame obj, CachedSaveGame loaded)
        //{
        //    if (loaded != null && targetsBySaveFile.ContainsKey(obj))
        //        targetsBySaveFile[obj].UpdateCachedData(loaded, GameSettingsViewModel.Load(obj).ModelObject);
        //}

        //private void CachedSaveGame_SaveFileLoadError(ISaveGame obj, Exception ex)
        //{
        //    logger.Error(ex, "error when parsing save file for {saveId}", CachedSaveGame.IdentifierFor(obj));

        //    var crashsupport = CrashSupport.PrepareSupportFile(specificSave: obj);
        //    AdonisMessageBox.Show(LocalizationCodes.LC_ERROR_SAVE_LOAD_FAILED.Bind(crashsupport).Value, caption: "");
        //}

        [NotifyCanExecuteChangedFor(nameof(BeginNavigateSaveSelectionPageCommand))]
        [ObservableProperty]
        private FrameworkElement content;
    }

    /// <summary>
    /// Interaction logic for AppWindow.xaml
    /// </summary>
    public partial class AppWindow : AdonisWindow
    {
        public AppWindow()
        {
            DataContext = new AppWindowViewModel(Dispatcher);
            InitializeComponent();
        }
    }
}
