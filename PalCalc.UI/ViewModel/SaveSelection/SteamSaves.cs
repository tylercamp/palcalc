using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal static class SteamSaves
    {
        public static SavesCollectionViewModel MakePlaceholderCollection()
        {
            return new SavesCollectionViewModel()
            {
                SaveType = SaveType.Steam,
                AvailableSaves = new([]),
                TypeLabel = new HardCodedText("Steam"), // TODO ITL
                Title = null,
                OpenFolderCommand = null,
                AddSaveCommand = null,
                RemoveSaveCommand = null
            };
        }

        public static List<SavesCollectionViewModel> CollectAll(
            IEnumerable<ISavesLocation> savesLocations
        )
        {
            return savesLocations
                .OfType<DirectSavesLocation>()
                .Select(FromLocation)
                .ToList();
        }

        public static SavesCollectionViewModel FromLocation(
            DirectSavesLocation location
        )
        {
            var res = new SavesCollectionViewModel()
            {
                SaveType = SaveType.Steam,
                TypeLabel = new HardCodedText("Steam"), // TODO ITL
                Title = new HardCodedText(location.FolderName),
                OpenFolderCommand = new RelayCommand(() => WindowsUtils.OpenPathInExplorer(location.FolderPath)),
                AddSaveCommand = null,
                RemoveSaveCommand = null
            };

            res.AvailableSaves = new ReadOnlyObservableCollection<SaveGameViewModel2>(
                [.. location.ValidSaveGames.OfType<StandardSaveGame>().Select(dsl => FromSave(res, dsl))]
            );

            return res;
        }

        public static SaveGameViewModel2 FromSave(SavesCollectionViewModel parent, StandardSaveGame save)
        {
            var res = SavesCommon.BuildNormalSave(
                parent: parent,
                save: save,
                openFolderCommand: new RelayCommand(() => WindowsUtils.OpenPathInExplorer(save.BasePath))
            );
            res.Type = SaveType.Steam;

            return res;
        }
    }
}
