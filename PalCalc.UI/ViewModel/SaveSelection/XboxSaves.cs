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
        public static List<SavesCollectionViewModel> CollectAll()
        {

        }

        public static SavesCollectionViewModel FromLocation(XboxSavesLocation location)
        {
            AvailableSaves = new ReadOnlyCollection<ISaveGameViewModel2>([.. savesLocation.AllSaveGames.OfType<XboxSaveGame>().Select(sg => new XboxSaveGameViewModel(sg))]);

            // TODO ITL
            TypeLabel = new HardCodedText("Xbox");
            Title = savesLocation.FolderPath != null ? new HardCodedText(((ISavesLocation)savesLocation).FolderName) : null;

            OpenFolderCommand = new RelayCommand(
                execute: () =>
                {
                    var fullPath = System.IO.Path.GetFullPath(savesLocation.FolderPath);
                    Process.Start("explorer.exe", fullPath);
                },
                canExecute: () => savesLocation?.FolderPath != null
            );
        }

        public static SaveGameViewModel2 FromSave(XboxSaveGame save)
        {
            var result = SavesCommon.BuildNormalSave(save);

            if (xboxSave.LevelMeta?.IsValid != true)
            {
                Warnings = [
                    LocalizationCodes.LC_SAVE_GAME_XBOX_INCOMPLETE.Bind(),
                ];
            }
        }
    }
}
