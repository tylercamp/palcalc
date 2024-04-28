using PalCalc.SaveReader;
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
        string Label { get; }
        DateTime? LastModified { get; }
    }

    public class StandardSavesLocationViewModel : ISavesLocationViewModel
    {
        public StandardSavesLocationViewModel(ISavesLocation sl)
        {
            Value = sl;

            var saveType = sl is XboxSavesLocation ? "Xbox" : "Steam";

            Label = $"{saveType} user {sl.FolderName.LimitLength(12)} - {sl.ValidSaveGames.Count()} valid saves";
            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(
                new ObservableCollection<SaveGameViewModel>(sl.ValidSaveGames.Select(sg => new SaveGameViewModel(sg)))
            );
            LastModified = sl.ValidSaveGames.OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;
        }

        public ISavesLocation Value { get; }

        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public string Label { get; }

        public DateTime? LastModified { get; }
    }

    public class ManualSavesLocationViewModel : ISavesLocationViewModel
    {
        public ManualSavesLocationViewModel(IEnumerable<ISaveGame> initialManualSaves)
        {
            saveGames = new ObservableCollection<SaveGameViewModel>(initialManualSaves.Select(s => new SaveGameViewModel(s)).OrderBy(vm => vm.Label));
            saveGames.Add(SaveGameViewModel.AddNewSave);

            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(saveGames);
        }

        private ObservableCollection<SaveGameViewModel> saveGames;
        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public string Label => "Manually Added";

        public DateTime? LastModified => saveGames.Where(g => !g.IsAddManualOption).OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;

        // assume `path` has already been validated
        public SaveGameViewModel Add(ISaveGame saveGame)
        {
            var vm = new SaveGameViewModel(saveGame);
            var orderedIndex = saveGames
                .Where(vm => !vm.IsAddManualOption)
                .Append(vm)
                .OrderBy(vm => vm.Label)
                .ToList()
                .IndexOf(vm);

            saveGames.Insert(orderedIndex, vm);

            return vm;
        }
    }
}
