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
        public static SavesCollectionViewModel MakePlaceholderCollection()
        {
            return new SavesCollectionViewModel()
            {
                SaveType = SaveType.Xbox,
                AvailableSaves = new([]),
                TypeLabel = new HardCodedText("Xbox"), // TODO ITL
                Title = null,
                OpenFolderCommand = null,
                AddSaveCommand = null,
                RemoveSaveCommand = null
            };
        }

        public static List<SavesCollectionViewModel> CollectAll(IEnumerable<ISavesLocation> saves)
        {
            return saves
                .OfType<XboxSavesLocation>()
                .Select(FromLocation)
                .ToList();
        }

        public static SavesCollectionViewModel FromLocation(XboxSavesLocation location)
        {
            var res = new SavesCollectionViewModel()
            {
                SourceLocation = location,
                SaveType = SaveType.Xbox,
                TypeLabel = new HardCodedText("Xbox"), // TODO ITL
                Title = location.FolderPath != null ? new HardCodedText(((ISavesLocation)location).FolderName) : null,
                OpenFolderCommand = location.FolderPath != null
                    ? new RelayCommand(() => WindowsUtils.OpenPathInExplorer(location.FolderPath))
                    : null,
                AddSaveCommand = null,
                RemoveSaveCommand = null
            };

            res.AvailableSaves = new([.. ((ISavesLocation)location).ValidSaveGames.OfType<XboxSaveGame>().Select(sg => FromSave(res, sg))]);

            return res;
        }

        public static SaveGameViewModel2 FromSave(SavesCollectionViewModel parent, XboxSaveGame save)
        {
            var result = SavesCommon.BuildNormalSave(
                parent: parent,
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
