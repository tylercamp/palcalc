using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public interface ISavesLocationViewModel
    {
        ReadOnlyObservableCollection<ISaveGameViewModel> SaveGames { get; }
        ILocalizedText Label { get; }
        DateTime? LastModified { get; }
    }

    public partial class StandardSavesLocationViewModel : ObservableObject, ISavesLocationViewModel
    {
        public StandardSavesLocationViewModel(ISavesLocation sl)
        {
            Value = sl;

            var isXbox = sl is XboxSavesLocation;

            SaveGames = new ReadOnlyObservableCollection<ISaveGameViewModel>(
                new ObservableCollection<ISaveGameViewModel>(sl.ValidSaveGames.Select(sg => new SaveGameViewModel(sg)))
            );

            if (sl.FolderPath == null)
            {
                Label = isXbox
                    ? LocalizationCodes.LC_SAVE_LOCATION_XBOX_EMPTY.Bind()
                    : LocalizationCodes.LC_SAVE_LOCATION_STEAM_EMPTY.Bind();
            }
            else
            {
                var baseText = isXbox
                    ? LocalizationCodes.LC_SAVE_LOCATION_LBL_XBOX
                    : LocalizationCodes.LC_SAVE_LOCATION_LBL_STEAM;

                Label = baseText.Bind(new {
                    UserId = sl.FolderName.LimitLength(12),
                    NumValidSaves = SaveGames.Count,
                });
            }
            
            LastModified = SaveGames.OfType<SaveGameViewModel>().OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;
        }

        public ISavesLocation Value { get; }

        public ReadOnlyObservableCollection<ISaveGameViewModel> SaveGames { get; }

        public ILocalizedText Label { get; }

        public DateTime? LastModified { get; }
    }

    public partial class ManualSavesLocationViewModel : ObservableObject, ISavesLocationViewModel
    {
        public ManualSavesLocationViewModel(IEnumerable<ISaveGame> initialManualSaves)
        {
            saveGames = new ObservableCollection<ISaveGameViewModel>(initialManualSaves.Select(s => new SaveGameViewModel(s)).OrderByDescending(vm => vm.LastModified));
            saveGames.Add(new NewManualSaveGameViewModel());
            saveGames.Add(new NewFakeSaveGameViewModel());


            SaveGames = new ReadOnlyObservableCollection<ISaveGameViewModel>(saveGames);
        }

        private ObservableCollection<ISaveGameViewModel> saveGames;
        public ReadOnlyObservableCollection<ISaveGameViewModel> SaveGames { get; }

        public ILocalizedText Label { get; } = LocalizationCodes.LC_SAVE_LOCATION_MANUAL.Bind();

        public DateTime? LastModified => saveGames.OfType<SaveGameViewModel>().OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;

        // assume `path` has already been validated
        public SaveGameViewModel Add(ISaveGame saveGame)
        {
            var vm = new SaveGameViewModel(saveGame);
            var orderedIndex = saveGames
                .OfType<SaveGameViewModel>()
                .Append(vm)
                .OrderBy(vm => vm.Label.Value)
                .ToList()
                .IndexOf(vm);

            saveGames.Insert(orderedIndex, vm);

            return vm;
        }

        public void Remove(ISaveGame saveGame)
        {
            saveGames.Remove(saveGames.OfType<SaveGameViewModel>().Single(g => g.Value == saveGame));
        }
    }
}
