using Microsoft.Xaml.Behaviors.Core;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal static class ManualSaves
    {
        public static SavesCollectionViewModel FromList(IEnumerable<StandardSaveGame> existingSaves, ISavesService savesService)
        {
            var removeSaveCommand = new ActionCommand((save) =>
            {

            });

            var availableSaves = new ObservableCollection<SaveGameViewModel2>([
                .. existingSaves.Select(sg => FromSave(sg, savesService))
            ]);


            return new SavesCollectionViewModel(
                SaveType: SaveType.LocalFile,
                AvailableSaves: new(availableSaves),
                TypeLabel: new HardCodedText("Local Files"), // TODO ITL
                Title: null,
                OpenFolderCommand: null,
                AddSaveCommand: null, // TODO
                RemoveSaveCommand: null // TODO
            );
        }

        public static SaveGameViewModel2 FromSave(StandardSaveGame save, ISavesService savesService)
        {
            return SavesCommon.BuildNormalSave(
                save: save,
                openFolderCommand: null // TODO
            );
        }
    }
}
