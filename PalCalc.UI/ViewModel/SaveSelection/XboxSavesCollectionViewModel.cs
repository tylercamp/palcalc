using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal class XboxSavesCollectionViewModel(XboxSavesLocation savesLocation) : ISavesCollectionViewModel
    {
        public IReadOnlyCollection<SaveGameViewModel> AvailableSaves { get; } =
            new ReadOnlyCollection<SaveGameViewModel>([.. savesLocation.AllSaveGames.Select(sg => new SaveGameViewModel(savesLocation, sg))]);

        // TODO ITL
        public ILocalizedText TypeLabel { get; } = new HardCodedText("Xbox");

        public ILocalizedText Title { get; } = 
            savesLocation.FolderPath != null ? new HardCodedText(((ISavesLocation)savesLocation).FolderName) : null;
    }
}
