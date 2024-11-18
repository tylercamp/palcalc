using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile.Virtual;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Mapped;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.UI.WebUI;

namespace PalCalc.UI.ViewModel
{
    internal partial class SaveSelectorViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<SaveSelectorViewModel>();

        public event Action<ManualSavesLocationViewModel, ISaveGame> NewCustomSaveSelected;
        public event Action<ManualSavesLocationViewModel, ISaveGame> CustomSaveDelete;

        public List<ISavesLocationViewModel> SavesLocations { get; }

        private ManualSavesLocationViewModel manualLocation;
        private ISavesLocationViewModel selectedLocation;
        public ISavesLocationViewModel SelectedLocation
        {
            get => selectedLocation;
            set
            {
                if (SetProperty(ref selectedLocation, value))
                {
                    OnPropertyChanged(nameof(CanOpenSavesLocation));
                    OnPropertyChanged(nameof(NoXboxSavesMsgVisibility));
                    OnPropertyChanged(nameof(AvailableSaves));
                    OnPropertyChanged(nameof(DeleteSaveVisibility));
                    SelectedGame = MostRecentSave;
                }
            }
        }

        public SaveGameViewModel SelectedFullGame => SelectedGame as SaveGameViewModel;

        private ISaveGameViewModel selectedGameOption;
        public ISaveGameViewModel SelectedGame
        {
            get => selectedGameOption;
            set
            {
                bool needsReset = false;

                switch (value)
                {
                    case null: break;

                    case NewManualSaveGameViewModel:
                        var ofd = new OpenFileDialog();
                        ofd.Filter = LocalizationCodes.LC_MANUAL_SAVE_EXTENSION_LBL.Bind().Value + "|Level.sav";
                        ofd.Title = LocalizationCodes.LC_MANUAL_SAVE_SELECTOR_TITLE.Bind().Value;

                        if (true == ofd.ShowDialog(App.Current.MainWindow))
                        {
                            var asSaveGame = new StandardSaveGame(Path.GetDirectoryName(ofd.FileName));
                            if (asSaveGame.IsValid)
                            {
                                var existingSaves = SavesLocations.SelectMany(l => l.SaveGames.OfType<SaveGameViewModel>().Select(vm => vm.Value)).SkipNull();
                                if (existingSaves.Any(s => s.BasePath.PathEquals(asSaveGame.BasePath)))
                                {
                                    MessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_MANUAL_SAVE_ALREADY_REGISTERED.Bind().Value);
                                }
                                else
                                {
                                    // leave updates + selection of the new location to the event handler
                                    Dispatcher.CurrentDispatcher.BeginInvoke(() => NewCustomSaveSelected?.Invoke(manualLocation, asSaveGame));
                                }
                            }
                            else
                            {
                                MessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_MANUAL_SAVE_INCOMPLETE.Bind().Value);
                                needsReset = true;
                            }
                        }
                        else
                        {
                            needsReset = true;
                        }
                        break;

                    case NewFakeSaveGameViewModel:
                        var existingFakeSaves = manualLocation.SaveGames
                            .OfType<SaveGameViewModel>()
                            .Select(sgvm => sgvm.Value)
                            .OfType<VirtualSaveGame>()
                            .Select(FakeSaveGame.GetLabel)
                            .ToList();

                        var window = new SimpleTextInputWindow()
                        {
                            Title = LocalizationCodes.LC_CUSTOM_SAVE_GAME_NAME.Bind().Value,
                            InputLabel = LocalizationCodes.LC_CUSTOM_SAVE_GAME_NAME_LABEL.Bind().Value,
                            Validator = name => name.Length > 0 && !existingFakeSaves.Contains(name),
                            Owner = App.ActiveWindow,
                        };

                        if (window.ShowDialog() == true)
                        {
                            var saveGame = FakeSaveGame.Create(window.Result);
                            Dispatcher.CurrentDispatcher.BeginInvoke(() => NewCustomSaveSelected?.Invoke(manualLocation, saveGame));
                        }
                        else
                        {
                            needsReset = true;
                        }
                        break;
                }

                CrashSupport.ReferencedSave((value as SaveGameViewModel)?.Value);

                if (SetProperty(ref selectedGameOption, value))
                {
                    OnPropertyChanged(nameof(XboxIncompleteVisibility));
                    OnPropertyChanged(nameof(CanOpenSaveFileLocation));
                    OnPropertyChanged(nameof(DeleteSaveVisibility));
                    OnPropertyChanged(nameof(SelectedFullGame));

                    if (needsReset)
                    {
                        // ComboBox ignores reassignment in the middle of a value-change event, defer until later
                        Dispatcher.CurrentDispatcher.BeginInvoke(() => SelectedGame = null);
                    }

                    ExportSaveCommand?.NotifyCanExecuteChanged();
                    InspectSaveCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public ReadOnlyObservableCollection<ISaveGameViewModel> AvailableSaves => selectedLocation.SaveGames;

        private ISavesLocationViewModel MostRecentLocation => SavesLocations.OrderByDescending(l => l.LastModified).FirstOrDefault();
        private SaveGameViewModel MostRecentSave => SelectedLocation?.SaveGames?.OfType<SaveGameViewModel>()?.OrderByDescending(s => s.LastModified)?.FirstOrDefault();

        public SaveSelectorViewModel() : this(DirectSavesLocation.AllLocal, Enumerable.Empty<ISaveGame>())
        {
        }

        public bool CanOpenSavesLocation => (SelectedLocation as StandardSavesLocationViewModel)?.Value?.FolderPath != null;
        public bool CanOpenSaveFileLocation => SelectedFullGame?.Value?.BasePath != null;

        public Visibility NoXboxSavesMsgVisibility => (SelectedLocation as StandardSavesLocationViewModel)?.Value is XboxSavesLocation && !SelectedLocation.SaveGames.Any() ? Visibility.Visible : Visibility.Collapsed;
        public Visibility XboxIncompleteVisibility => SelectedFullGame != null && SelectedFullGame.Value is XboxSaveGame && (SelectedFullGame.Value as XboxSaveGame).LevelMeta?.IsValid != true ? Visibility.Visible : Visibility.Collapsed;

        public SaveSelectorViewModel(IEnumerable<ISavesLocation> savesLocations, IEnumerable<ISaveGame> manualSaves)
        {
            manualLocation = new ManualSavesLocationViewModel(manualSaves);

            SavesLocations = new List<ISavesLocationViewModel>(savesLocations.Select(l => new StandardSavesLocationViewModel(l)).OrderByDescending(vm => vm.LastModified));
            SavesLocations.Add(manualLocation);

            SelectedLocation = MostRecentLocation;

            ExportSaveCommand = new RelayCommand(
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

                            if (save.Level != null && save.Level.Exists)
                                archive.CreateEntryFromFile(save.Level.FilePath, "Level.sav");

                            if (save.LevelMeta != null && save.LevelMeta.Exists)
                                archive.CreateEntryFromFile(save.LevelMeta.FilePath, "LevelMeta.sav");

                            if (save.WorldOption != null && save.WorldOption.Exists)
                                archive.CreateEntryFromFile(save.WorldOption.FilePath, "WorldOption.sav");

                            if (save.LocalData != null && save.LocalData.Exists)
                                archive.CreateEntryFromFile(save.LocalData.FilePath, "LocalData.sav");

                            foreach (var player in save.Players.Where(p => p.Exists))
                            {
                                archive.CreateEntryFromFile(player.FilePath, $"Players/{Path.GetFileName(player.FilePath)}");
                            }
                        }
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
                            MessageBox.Show(LocalizationCodes.LC_CRASHLOG_FAILED.Bind().Value);
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
                    loadingModal.ShowSync();

                    var vm = new SaveInspectorWindowViewModel(SelectedFullGame);

                    loadingModal.Close();

                    var inspector = new SaveInspectorWindow() { DataContext = vm, Owner = App.Current.MainWindow };
                    inspector.Show();
                },
                canExecute: () => SelectedFullGame?.CachedValue != null
            );

            DeleteSaveCommand = new RelayCommand(
                () => CustomSaveDelete?.Invoke(manualLocation, SelectedFullGame.Value)
            );
        }

        public void TrySelectSaveGame(string saveIdentifier)
        {
            foreach (var loc in SavesLocations)
            {
                foreach (var game in loc.SaveGames.OfType<SaveGameViewModel>().Where(g => g.Value != null))
                {
                    if (CachedSaveGame.IdentifierFor(game.Value) == saveIdentifier)
                    {
                        SelectedLocation = loc;
                        SelectedGame = game;
                        return;
                    }
                }
            }
        }

        private IRelayCommand exportSaveCommand;
        public IRelayCommand ExportSaveCommand
        {
            get => exportSaveCommand;
            private set => SetProperty(ref exportSaveCommand, value);
        }

        private IRelayCommand exportCrashLogCommand;
        public IRelayCommand ExportCrashLogCommand
        {
            get => exportCrashLogCommand;
            private set => SetProperty(ref exportCrashLogCommand, value);
        }

        [ObservableProperty]
        private IRelayCommand deleteSaveCommand;

        [ObservableProperty]
        private IRelayCommand inspectSaveCommand;

        public Visibility DeleteSaveVisibility =>
            SelectedLocation is ManualSavesLocationViewModel && SelectedFullGame != null
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}
