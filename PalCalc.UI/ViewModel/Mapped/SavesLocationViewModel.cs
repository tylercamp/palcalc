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
        public StandardSavesLocationViewModel(SavesLocation sl)
        {
            Value = sl;

            Label = $"{sl.ValidSaveGames.Count()} valid saves ({sl.FolderName})";
            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(
                new ObservableCollection<SaveGameViewModel>(sl.ValidSaveGames.Select(SaveGameViewModel.FromSave))
            );
            LastModified = sl.ValidSaveGames.OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;
        }

        public SavesLocation Value { get; }

        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public string Label { get; }

        public DateTime? LastModified { get; }
    }

    public class ManualSavesLocationViewModel : ISavesLocationViewModel
    {
        public ManualSavesLocationViewModel(IEnumerable<SaveGame> initialManualSaves)
        {
            saveGames = new ObservableCollection<SaveGameViewModel>(initialManualSaves.Select(SaveGameViewModel.FromSave).OrderBy(vm => vm.Label));
            saveGames.Add(SaveGameViewModel.AddNewSave);

            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(saveGames);
        }

        private ObservableCollection<SaveGameViewModel> saveGames;
        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public string Label => "Manually Added";

        public DateTime? LastModified => saveGames.Where(g => !g.IsAddManualOption).OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;

        // assume `saveGame` has already been validated
        public SaveGameViewModel Add(SaveGame saveGame)
        {
            var vm = SaveGameViewModel.FromSave(saveGame);
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
