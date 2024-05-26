using PalCalc.SaveReader;
using PalCalc.UI2.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.ViewModels.Mapped
{
    internal interface ISavesLocationVM
    {
        ReadOnlyObservableCollection<SaveGameInfoVM> SaveGames { get; }
        string Label { get; }
        DateTime? LastModified { get; }

        SaveGameVM DataFor(SaveGameInfoVM info);
    }

    internal class StandardSavesLocationVM : ISavesLocationVM
    {
        private List<SaveGameVM> saveData;

        public StandardSavesLocationVM(ISavesLocation sl, IEnumerable<SaveGameVM> initialAvailableSaves)
        {
            Value = sl;
            saveData = initialAvailableSaves.ToList();

            var saveType = sl is XboxSavesLocation ? "Xbox" : "Steam";

            if (sl.FolderPath == null)
            {
                Label = saveType;
            }
            else
            {
                Label = $"{saveType} user {sl.FolderName.LimitLength(12)} - {sl.ValidSaveGames.Count()} valid saves";
            }

            SaveGames = new ReadOnlyObservableCollection<SaveGameInfoVM>(
                new ObservableCollection<SaveGameInfoVM>(initialAvailableSaves.Select(s => s.Info))
            );
            LastModified = sl.ValidSaveGames.OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;
        }

        public ISavesLocation Value { get; }

        public ReadOnlyObservableCollection<SaveGameInfoVM> SaveGames { get; }

        public string Label { get; }

        public DateTime? LastModified { get; }

        public SaveGameVM DataFor(SaveGameInfoVM info) => saveData.Single(s => s.Info == info);
    }

    internal class AddNewManualSavePlaceholderInfo : PlaceholderSaveGameInfo
    {
        private AddNewManualSavePlaceholderInfo() : base("Add new save...")
        {
        }

        public static readonly AddNewManualSavePlaceholderInfo Instance = new();
    }

    internal class ManualSavesLocationVM : ISavesLocationVM
    {
        private List<SaveGameVM> saveData;

        public ManualSavesLocationVM(IEnumerable<SaveGameVM> initialManualSaves)
        {
            saveData = initialManualSaves.ToList();

            saveGames = new ObservableCollection<SaveGameInfoVM>(initialManualSaves.Select(s => s.Info).OrderBy(vm => vm.Label));
            saveGames.Add(new SaveGameInfoVM(null, AddNewManualSavePlaceholderInfo.Instance));

            SaveGames = new ReadOnlyObservableCollection<SaveGameInfoVM>(saveGames);
        }

        private ObservableCollection<SaveGameInfoVM> saveGames;
        public ReadOnlyObservableCollection<SaveGameInfoVM> SaveGames { get; }

        public string Label => "Manually Added";

        public DateTime? LastModified => saveData.OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;

        // assume `path` has already been validated
        public void Add(SaveGameVM saveGame)
        {
            var orderedIndex = saveGames
                .Where(vm => saveData.Any(d => d.Info == vm))
                .Append(saveGame.Info)
                .OrderBy(vm => vm.Label)
                .ToList()
                .IndexOf(saveGame.Info);

            saveGames.Insert(orderedIndex, saveGame.Info);
        }

        public SaveGameVM DataFor(SaveGameInfoVM info) => saveData.Single(s => s.Info == info);
    }
}
