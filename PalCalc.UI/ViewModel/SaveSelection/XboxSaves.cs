using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal static class XboxSaves
    {
        public static List<SavesCollectionViewModel> CollectAll(IEnumerable<ISavesLocation> saves)
        {
            return saves
                .OfType<XboxSavesLocation>()
                .Select(FromLocation)
                .ToList();
        }

        public static SavesCollectionViewModel FromLocation(XboxSavesLocation location)
        {
            return new SavesCollectionViewModel(
                SaveType: SaveType.Xbox,
                AvailableSaves: new([.. location.AllSaveGames.OfType<XboxSaveGame>().Select(FromSave)]),
                TypeLabel: new HardCodedText("Xbox"), // TODO ITL
                Title: location.FolderPath != null ? new HardCodedText(((ISavesLocation)location).FolderName) : null,
                OpenFolderCommand: location.FolderPath != null
                    ? new RelayCommand(() => WindowsUtils.OpenPathInExplorer(location.FolderPath))
                    : null,
                AddSaveCommand: null,
                RemoveSaveCommand: null
            );
        }

        public static SaveGameViewModel2 FromSave(XboxSaveGame save)
        {
            var result = SavesCommon.BuildNormalSave(
                save: save,
                openFolderCommand: null
            );

            result.Type = SaveType.Xbox;

            if (save.LevelMeta?.IsValid != true)
            {
                result.Warnings = [
                    LocalizationCodes.LC_SAVE_GAME_XBOX_INCOMPLETE.Bind(),
                ];
            }

            return result;
        }
    }
}
