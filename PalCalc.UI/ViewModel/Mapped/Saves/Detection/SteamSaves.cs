using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.SaveSelection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped.Saves.Detection
{
    internal static class SteamSaves
    {
        public static SavesCollectionViewModel MakePlaceholderCollection()
        {
            return new SavesCollectionViewModel()
            {
                SaveType = SaveType.Steam,
                AvailableSaves = new([]),
                TypeLabel = LocalizationCodes.LC_SAVE_KIND_TITLE_STEAM.Bind(),
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
            //return []; // for "no saves available" testing
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
                SourceLocation = location,
                SaveType = SaveType.Steam,
                TypeLabel = LocalizationCodes.LC_SAVE_KIND_TITLE_STEAM.Bind(),
                Title = new HardCodedText(location.FolderName),
                OpenFolderCommand = new RelayCommand(() => WindowsUtils.OpenPathInExplorer(location.FolderPath)),
                AddSaveCommand = null,
                RemoveSaveCommand = null
            };

            res.AvailableSaves = new ReadOnlyObservableCollection<SaveGameViewModel>(
                [.. location.ValidSaveGames.OfType<StandardSaveGame>().Select(dsl => FromSave(res, dsl))]
            );

            return res;
        }

        public static SaveGameViewModel FromSave(SavesCollectionViewModel parent, StandardSaveGame save)
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
