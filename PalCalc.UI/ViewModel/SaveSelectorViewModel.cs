using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.View;
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
                    SelectedGame = MostRecentSave;
                }
            }
        }

        private SaveGameViewModel selectedGame;
        public SaveGameViewModel SelectedGame
        {
            get => selectedGame;
            set
            {
                bool needsReset = false;
                if (value != null && value.IsAddManualOption)
                {
                    var ofd = new OpenFileDialog();
                    ofd.Filter = "Level save file|Level.sav";
                    ofd.Title = "Select the 'Level.sav' file in your save folder";

                    if (true == ofd.ShowDialog(App.Current.MainWindow))
                    {
                        var asSaveGame = new StandardSaveGame(Path.GetDirectoryName(ofd.FileName));
                        if (asSaveGame.IsValid)
                        {
                            var existingSaves = SavesLocations.SelectMany(l => l.SaveGames.Select(vm => vm.Value)).SkipNull();
                            if (existingSaves.Any(s => s.BasePath.PathEquals(asSaveGame.BasePath)))
                            {
                                MessageBox.Show(App.Current.MainWindow, "The selected file has already been registered");
                            }
                            else
                            {
                                // leave updates + selection of the new location to the event handler
                                Dispatcher.CurrentDispatcher.BeginInvoke(() => NewCustomSaveSelected?.Invoke(manualLocation, asSaveGame));
                            }
                        }
                        else
                        {
                            MessageBox.Show(App.Current.MainWindow, "The selected file is not in a complete save-game folder");
                            needsReset = true;
                        }
                    }
                    else
                    {
                        needsReset = true;
                    }
                }

                CrashSupport.ReferencedSave(value?.Value);

                if (SetProperty(ref selectedGame, value))
                {
                    OnPropertyChanged(nameof(XboxIncompleteVisibility));
                    OnPropertyChanged(nameof(CanOpenSaveFileLocation));
                    if (needsReset)
                    {
                        // ComboBox ignores reassignment in the middle of a value-change event, defer until later
                        Dispatcher.CurrentDispatcher.BeginInvoke(() => SelectedGame = null);
                    }
                    else
                    {
                        OnPropertyChanged(nameof(InvalidSaveMessageVisibility));
                    }

                    ExportSaveCommand?.NotifyCanExecuteChanged();
                    InspectSaveCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public ReadOnlyObservableCollection<SaveGameViewModel> AvailableSaves => selectedLocation.SaveGames;

        public Visibility InvalidSaveMessageVisibility => SelectedGame?.WarningVisibility ?? Visibility.Collapsed;

        private ISavesLocationViewModel MostRecentLocation => SavesLocations.OrderByDescending(l => l.LastModified).FirstOrDefault();
        private SaveGameViewModel MostRecentSave => SelectedLocation?.SaveGames?.Where(g => !g.IsAddManualOption)?.OrderByDescending(s => s.LastModified)?.FirstOrDefault();

        public SaveSelectorViewModel() : this(DirectSavesLocation.AllLocal, Enumerable.Empty<ISaveGame>())
        {
        }

        public bool CanOpenSavesLocation => (SelectedLocation as StandardSavesLocationViewModel)?.Value?.FolderPath != null;
        public bool CanOpenSaveFileLocation => (SelectedGame as SaveGameViewModel)?.Value?.BasePath != null;

        public Visibility NoXboxSavesMsgVisibility => (SelectedLocation as StandardSavesLocationViewModel)?.Value is XboxSavesLocation && !SelectedLocation.SaveGames.Any() ? Visibility.Visible : Visibility.Collapsed;
        public Visibility XboxIncompleteVisibility => SelectedGame != null && SelectedGame.Value is XboxSaveGame && (SelectedGame.Value as XboxSaveGame).LevelMeta?.IsValid != true ? Visibility.Visible : Visibility.Collapsed;

        public SaveSelectorViewModel(IEnumerable<ISavesLocation> savesLocations, IEnumerable<ISaveGame> manualSaves)
        {
            manualLocation = new ManualSavesLocationViewModel(manualSaves);

            SavesLocations = new List<ISavesLocationViewModel>(savesLocations.Select(l => new StandardSavesLocationViewModel(l)).OrderBy(vm => vm.Label));
            SavesLocations.Add(manualLocation);

            SelectedLocation = MostRecentLocation;

            ExportSaveCommand = new RelayCommand(
                execute: () =>
                {
                    var sfd = new SaveFileDialog()
                    {
                        FileName = $"Palworld-{CachedSaveGame.IdentifierFor(SelectedGame.Value)}.zip",
                        Filter = "ZIP | *.zip",
                        AddExtension = true,
                        DefaultExt = "zip"
                    };

                    if (sfd.ShowDialog() == true)
                    {
                        using (var outStream = new FileStream(sfd.FileName, FileMode.Create))
                        using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create))
                        {
                            var save = SelectedGame.Value;

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
                canExecute: () => SelectedGame?.Value != null
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
                            MessageBox.Show("Could not create the crashlog file.");
                        }
                    }
                }
            );

            InspectSaveCommand = new RelayCommand(
                execute: () =>
                {
                    var vm = new SaveInspectorViewModel(SelectedGame.CachedValue);
                    var inspector = new SaveInspectorWindow() { DataContext = vm, Owner = App.Current.MainWindow };
                    inspector.ShowDialog();
                },
                canExecute: () => SelectedGame != null && !SelectedGame.IsAddManualOption && SelectedGame?.CachedValue != null
            );
        }

        public void TrySelectSaveGame(string saveIdentifier)
        {
            foreach (var loc in SavesLocations)
            {
                foreach (var game in loc.SaveGames.Where(g => g.Value != null))
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
        private IRelayCommand inspectSaveCommand;
    }
}
