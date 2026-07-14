using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal static class VirtualSaves
    {
        public static SavesCollectionViewModel FromList(IEnumerable<VirtualSaveGame> saves, ISavesService savesService)
        {
            var availableSaves = new ObservableCollection<SaveGameViewModel2>(
                [.. saves.Select(fs => FromSave(fs, savesService))]
            );

            return new SavesCollectionViewModel(
                SaveType: SaveType.Virtual,
                AvailableSaves: new(availableSaves),
                TypeLabel: new HardCodedText("Fake Saves"), // TODO ITL
                Title: null,
                OpenFolderCommand: null,
                AddSaveCommand: null, // TODO
                RemoveSaveCommand: null // TODO
            );
        }

        public static SaveGameViewModel2 FromSave(VirtualSaveGame save, ISavesService savesService)
        {
            var meta = save.LevelMeta.ReadGameOptions();

            return new SaveGameViewModel2()
            {
                ModelObject = save,
                IsValid = true,
                CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(
                    // (InGameDay is always default 0, but the format param is required for this translation code)
                    new { WorldName = meta.WorldName, DayNumber = meta.InGameDay }
                ),
                Warnings = [],
                WorldName = meta.WorldName,
                DayNumber = meta.InGameDay,
                MainPlayerName = null,
                MainPlayerLevel = null,
                OpenFolderCommand = null
            };
        }
    }
}
