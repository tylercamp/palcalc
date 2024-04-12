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
    internal partial class SavesLocationViewModel
    {
        public SavesLocationViewModel(SavesLocation sl)
        {
            Value = sl;

            Label = $"{sl.ValidSaveGames.Count()} valid saves ({sl.FolderName})";
            SaveGames = sl.ValidSaveGames.Select(sg => new SaveGameViewModel(sg)).ToList();
            LastModified = SaveGames.OrderByDescending(sg => sg.LastModified).FirstOrDefault()?.LastModified;
        }

        public DateTime? LastModified { get; }

        public List<SaveGameViewModel> SaveGames { get; }

        public SavesLocation Value { get; }
        public string Label { get; }
    }

    internal partial class SaveGameViewModel
    {
        public SaveGameViewModel(SaveGame value)
        {
            Value = value;

            try
            {
                var meta = value.LevelMeta.ReadGameOptions();
                Label = $"{meta.PlayerName} lv. {meta.PlayerLevel} in {meta.WorldName}";
            }
            catch (Exception ex)
            {
                // TODO - log exception
                Label = $"{value.FolderName} (Unable to read metadata)";
            }
        }

        public DateTime LastModified => Value.LastModified;

        public SaveGame Value { get; }
        public string Label { get; }
    }

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
