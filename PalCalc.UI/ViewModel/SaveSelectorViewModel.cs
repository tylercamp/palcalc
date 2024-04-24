using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel
{
    internal partial class SaveSelectorViewModel : ObservableObject
    {
        public event Action<ManualSavesLocationViewModel, SaveGame> NewCustomSaveSelected;

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
                        var asSaveGame = new SaveGame(Path.GetDirectoryName(ofd.FileName));
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

                if (SetProperty(ref selectedGame, value))
                {
                    if (needsReset)
                    {
                        // ComboBox ignores reassignment in the middle of a value-change event, defer until later
                        Dispatcher.CurrentDispatcher.BeginInvoke(() => SelectedGame = null);
                    }
                    else
                    {
                        OnPropertyChanged(nameof(InvalidSaveMessageVisibility));
                    }
                }
            }
        }

        public ReadOnlyObservableCollection<SaveGameViewModel> AvailableSaves => selectedLocation.SaveGames;

        public Visibility InvalidSaveMessageVisibility => SelectedGame?.WarningVisibility ?? Visibility.Collapsed;

        private ISavesLocationViewModel MostRecentLocation => SavesLocations.OrderByDescending(l => l.LastModified).FirstOrDefault();
        private SaveGameViewModel MostRecentSave => SelectedLocation?.SaveGames?.Where(g => !g.IsAddManualOption)?.OrderByDescending(s => s.LastModified)?.FirstOrDefault();

        public SaveSelectorViewModel() : this(SavesLocation.AllLocal, Enumerable.Empty<SaveGame>())
        {
        }

        public SaveSelectorViewModel(IEnumerable<SavesLocation> savesLocations, IEnumerable<SaveGame> manualSaves)
        {
            manualLocation = new ManualSavesLocationViewModel(manualSaves);

            SavesLocations = new List<ISavesLocationViewModel>(savesLocations.Select(l => new StandardSavesLocationViewModel(l)).OrderBy(vm => vm.Label));
            SavesLocations.Add(manualLocation);

            SelectedLocation = MostRecentLocation;
        }
    }
}
