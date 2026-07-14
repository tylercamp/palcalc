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
        public static List<SavesCollectionViewModel> CollectAll(
            IEnumerable<ISavesLocation> savesLocations,
            ISavesService savesService
        )
        {
            return savesLocations
                .OfType<DirectSavesLocation>()
                .Select(dsl => FromLocation(dsl, savesService))
                .ToList();
        }

        public static SavesCollectionViewModel FromLocation(
            DirectSavesLocation location,
            ISavesService savesService
        )
        {
            return new SavesCollectionViewModel(
                SaveType: SaveType.Steam,
                AvailableSaves: new ReadOnlyObservableCollection<SaveGameViewModel2>(
                    [.. location.ValidSaveGames.OfType<StandardSaveGame>().Select(dsl => FromSave(dsl, savesService))]
                ),
                TypeLabel: new HardCodedText("Steam"), // TODO ITL
                Title: new HardCodedText(location.FolderName),
                OpenFolderCommand: null, // TODO
                AddSaveCommand: null,
                RemoveSaveCommand: null
            );
        }

        public static SaveGameViewModel2 FromSave(StandardSaveGame save, ISavesService savesService) =>
            SavesCommon.BuildNormalSave(
                save: save,
                openFolderCommand: null // TODO
            );
    }
}
