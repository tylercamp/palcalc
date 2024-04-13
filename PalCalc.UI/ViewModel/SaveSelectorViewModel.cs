using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    internal partial class SaveSelectorViewModel : ObservableObject
    {
        [ObservableProperty]
        private List<SavesLocationViewModel> savesLocations;

        private SavesLocationViewModel selectedLocation;
        public SavesLocationViewModel SelectedLocation
        {
            get => selectedLocation;
            set
            {
                if (selectedLocation == value) return;

                selectedLocation = value;

                OnPropertyChanged(nameof(SelectedLocation));
                SelectedGame = MostRecentSave;
            }
        }

        private SaveGameViewModel selectedGame;
        public SaveGameViewModel SelectedGame
        {
            get => selectedGame;
            set
            {
                if (value == selectedGame) return;

                selectedGame = value;
                OnPropertyChanged(nameof(SelectedGame));
                OnSelectedSaveChanged?.Invoke(value);
            }
        }

        public List<SaveGameViewModel> AvailableSaves => selectedLocation.SaveGames;

        private SavesLocationViewModel MostRecentLocation => SavesLocations.OrderByDescending(l => l.LastModified).FirstOrDefault();
        private SaveGameViewModel MostRecentSave => SelectedLocation?.SaveGames?.OrderByDescending(s => s.LastModified)?.FirstOrDefault();

        public SaveSelectorViewModel() : this(SavesLocation.AllLocal)
        {
        }

        public SaveSelectorViewModel(IEnumerable<SavesLocation> savesLocations)
        {
            this.savesLocations = savesLocations.Select(sl => new SavesLocationViewModel(sl)).ToList();
            SelectedLocation = MostRecentLocation;
        }

        public event Action<SaveGameViewModel> OnSelectedSaveChanged;
    }
}
