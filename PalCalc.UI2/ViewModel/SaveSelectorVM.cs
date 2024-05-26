using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI2.Model;
using PalCalc.UI2.ViewModels.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI2.ViewModel
{
    internal partial class SaveSelectorVM : ObservableObject
    {
        public List<ISavesLocationVM> SavesLocations { get; }

        private ManualSavesLocationVM manualLocation;
        private ISavesLocationVM selectedLocation;
        public ISavesLocationVM SelectedLocation
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

        private SaveGameInfoVM selectedGame;
        public SaveGameInfoVM SelectedGame
        {
            get => selectedGame;
            set => SetProperty(ref selectedGame, value);
        }

        public ReadOnlyObservableCollection<SaveGameInfoVM> AvailableSaves => selectedLocation.SaveGames;

        //public Visibility InvalidSaveMessageVisibility => SelectedGame?.WarningVisibility ?? Visibility.Collapsed;

        private ISavesLocationVM MostRecentLocation => SavesLocations.OrderByDescending(l => l.LastModified).FirstOrDefault();
        private SaveGameInfoVM MostRecentSave => SelectedLocation?.SaveGames?.Where(g => g.Source is not PlaceholderSaveGameInfo).OrderByDescending(s => s.Source?.SourceLastModified)?.FirstOrDefault();

        public SaveSelectorVM() : this(null, DirectSavesLocation.AllLocal.Select(l => new StandardSavesLocationVM(l, l.ValidSaveGames.Select(g => new SaveGameVM(g)).ToList())), Enumerable.Empty<SaveGameVM>())
        {
        }

        public SaveSelectorVM(IRelayCommand<SaveGameInfoVM> addManualSaveCommand, IEnumerable<ISavesLocationVM> savesLocations, IEnumerable<SaveGameVM> manualSaves)
        {
            AddManualSaveCommand = addManualSaveCommand;

            manualLocation = new ManualSavesLocationVM(manualSaves);

            SavesLocations = savesLocations.ToList();
            SavesLocations.Add(manualLocation);

            SelectedLocation = MostRecentLocation;
        }

        public IRelayCommand<SaveGameInfoVM> AddManualSaveCommand { get; }

        public IRelayCommand<ISavesLocationVM> OpenSaveFolderCommand { get; }
        public IRelayCommand<SaveGameInfoVM> OpenGameFolderCommand { get; }
    }
}
