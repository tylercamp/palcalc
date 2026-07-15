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

        public AppWindowViewModel(Dispatcher dispatcher)
        {
            AppSettings.Current = settings = Storage.LoadAppSettings();
            savesService = new AppSettingsSaveService(settings);
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

            //CachedSaveGame.SaveFileLoadEnd += CachedSaveGame_SaveFileLoadEnd;
            //CachedSaveGame.SaveFileLoadError += CachedSaveGame_SaveFileLoadError;



            //if (settings.SelectedGameIdentifier != null) SaveSelection.TrySelectSaveGame(settings.SelectedGameIdentifier);

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

        private void BeginNavigateSaveSelectionPage()
        {
            var loadingPage = new LoadingPage();
            Content = loadingPage;

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

        private void NavigateSaveSelectionPage(IEnumerable<SavesCollectionViewModel> collections)
        {
            var vm = new SaveSelectionOnboardingViewModel(
                savesCollections: collections,
                loadSaveCommand: new RelayCommand<SaveGameViewModel2>(NavigateSolverPage)
            );

            var page = new SaveSelectionOnboardingPage();
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

            var vm = new SolverPageViewModel(Dispatcher.CurrentDispatcher, selectedSave, LoadPalTargets(selectedSave));
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

        /*
         * ExportSaveCommand = new RelayCommand(
                execute: () =>
                {
                    var sfd = new SaveFileDialog()
                    {
                        FileName = $"Palworld-{CachedSaveGame.IdentifierFor(SelectedFullGame.Value)}.zip",
                        Filter = "ZIP | *.zip",
                        AddExtension = true,
                        DefaultExt = "zip"
                    };

                    if (sfd.ShowDialog() == true)
                    {
                        using (var outStream = new FileStream(sfd.FileName, FileMode.Create))
                        using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create))
                        {
                            var save = SelectedFullGame.Value;

                            void Export(ISaveFile file, string basePath)
                            {
                                var filePaths = file.FilePaths.ToArray();
                                if (filePaths.Length == 1)
                                {
                                    archive.CreateEntryFromFile(filePaths[0], $"{basePath}.sav");
                                }
                                else
                                {
                                    for (int i = 0; i < filePaths.Length; i++)
                                    {
                                        archive.CreateEntryFromFile(filePaths[i], $"{basePath}-{i}.sav");
                                    }
                                }
                            }

                            void ExportRaw(SaveFileLocation rawFile)
                            {
                                archive.CreateEntryFromFile(rawFile.ActualPath, $"_raw_/{rawFile.NormalizedPath}");
                            }

                            if (save.Level != null && save.Level.Exists)
                                Export(save.Level, "Level");

                            if (save.LevelMeta != null && save.LevelMeta.Exists)
                                Export(save.LevelMeta, "LevelMeta");

                            if (save.WorldOption != null && save.WorldOption.Exists)
                                Export(save.WorldOption, "WorldOption");

                            if (save.LocalData != null && save.LocalData.Exists)
                                Export(save.LocalData, "LocalData");

                            foreach (var player in save.Players.Where(p => p.Exists))
                            {
                                string playerId;
                                try
                                {
                                    playerId = player.ReadPlayerContent().PlayerId;
                                }
                                catch
                                {
                                    playerId = Path.GetFileNameWithoutExtension(player.FilePaths.First());
                                }
                                Export(player, $"Players/{playerId}");
                            }

                            foreach (var rawFile in save.RawFiles)
                                ExportRaw(rawFile);
                        }
                    }
                },
                canExecute: () => SelectedFullGame?.Value != null
            );

            ExportSaveCsvCommand = new RelayCommand(
                execute: () =>
                {
                    var sfd = new SaveFileDialog()
                    {
                        FileName = $"Pals.csv",
                        Filter = "CSV | *.csv",
                        AddExtension = true,
                        DefaultExt = "csv"
                    };

                    if (sfd.ShowDialog() == true)
                    {
                        File.WriteAllText(sfd.FileName, PalCSVExporter.Export(SelectedFullGame.CachedValue, GameSettingsViewModel.Load(SelectedFullGame.Value).ModelObject));
                    }
                },
                canExecute: () => SelectedFullGame?.Value != null
            );

            ExportCrashLogCommand = new RelayCommand(
                execute: () =>
                {
                    var sfd = new SaveFileDialog();
                    sfd.FileName = "CRASHLOG.zip";
                    sfd.Filter = "ZIP | *.zip";
                    sfd.AddExtension = true;
                    sfd.DefaultExt = "zip";

                    if (sfd.ShowDialog() == true)
                    {
                        try
                        {
                            CrashSupport.PrepareSupportFile(sfd.FileName);
                        }
                        catch (Exception e)
                        {
                            logger.Warning(e, "unexpected error when attempting to create crashlog file");
                            AdonisMessageBox.Show(LocalizationCodes.LC_CRASHLOG_FAILED.Bind().Value, caption: "");
                        }
                    }
                }
            );

            InspectSaveCommand = new RelayCommand(
                execute: () =>
                {
                    var loadingModal = new LoadingSaveFileModal();
                    loadingModal.Owner = App.Current.MainWindow;
                    loadingModal.DataContext = LocalizationCodes.LC_SAVE_INSPECTOR_LOADING.Bind();

                    var vm = loadingModal.ShowDialogDuring(
                        () => new SaveInspectorWindowViewModel(SelectedLocation, SelectedFullGame, GameSettingsViewModel.Load(SelectedFullGame.Value).ModelObject)
                    );

                    var inspector = new SaveInspectorWindow() { DataContext = vm, Owner = App.Current.MainWindow };
                    inspector.Show();
                },
                canExecute: () => SelectedFullGame?.CachedValue != null
            );

            DeleteSaveCommand = new RelayCommand(
                () => CustomSaveDelete?.Invoke(manualLocation, SelectedFullGame.Value)
            );
        */

        [ObservableProperty]
        private Page content;
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
