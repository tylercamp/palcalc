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
        ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }
        ILocalizedText Label { get; }
        DateTime? LastModified { get; }
    }

    public class StandardSavesLocationViewModel : ISavesLocationViewModel
    {
        public StandardSavesLocationViewModel(ISavesLocation sl)
        {
            Value = sl;

            var isXbox = sl is XboxSavesLocation;

            if (sl.FolderPath == null)
            {
                Label = isXbox
                    ? LocalizationCodes.LC_SAVE_LOCATION_XBOX_EMPTY.Bind()
                    : LocalizationCodes.LC_SAVE_LOCATION_STEAM_EMPTY.Bind(); ;
            }
            else
            {
                var baseText = isXbox
                    ? LocalizationCodes.LC_SAVE_LOCATION_LBL_XBOX
                    : LocalizationCodes.LC_SAVE_LOCATION_LBL_STEAM;

                Label = baseText.Bind(new {
                    UserId = sl.FolderName.LimitLength(12),
                    NumValidSaves = sl.ValidSaveGames.Count(),
                });
            }
            
            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(
                new ObservableCollection<SaveGameViewModel>(sl.ValidSaveGames.Select(sg => new SaveGameViewModel(sg)))
            );
            LastModified = sl.ValidSaveGames.OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;
        }

        public ISavesLocation Value { get; }

        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public ILocalizedText Label { get; }

        public DateTime? LastModified { get; }
    }

    public class ManualSavesLocationViewModel : ISavesLocationViewModel
    {
        public ManualSavesLocationViewModel(IEnumerable<ISaveGame> initialManualSaves)
        {
            saveGames = new ObservableCollection<SaveGameViewModel>(initialManualSaves.Select(s => new SaveGameViewModel(s)).OrderByDescending(vm => vm.LastModified));
            saveGames.Add(SaveGameViewModel.AddNewSave);

            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(saveGames);
        }

        private ObservableCollection<SaveGameViewModel> saveGames;
        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public ILocalizedText Label { get; } = LocalizationCodes.LC_SAVE_LOCATION_MANUAL.Bind();

        public DateTime? LastModified => saveGames.Where(g => !g.IsAddManualOption).OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;

        // assume `path` has already been validated
        public SaveGameViewModel Add(ISaveGame saveGame)
        {
            var vm = new SaveGameViewModel(saveGame);
            var orderedIndex = saveGames
                .Where(vm => !vm.IsAddManualOption)
                .Append(vm)
                .OrderBy(vm => vm.Label.Value)
                .ToList()
                .IndexOf(vm);

            saveGames.Insert(orderedIndex, vm);

            return vm;
        }
    }
}
