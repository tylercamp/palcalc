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
    // (only Steam saves are currently supported for auto-detection as "Direct Saves")
    internal class SteamSavesCollectionViewModel(DirectSavesLocation steamLocation) : ISavesCollectionViewModel
    {
        public IReadOnlyCollection<SaveGameViewModel> AvailableSaves { get; } =
            new ReadOnlyCollection<SaveGameViewModel>([.. steamLocation.ValidSaveGames.Select(sg => new SaveGameViewModel(steamLocation, sg))]);

        // TODO ITL
        public ILocalizedText TypeLabel { get; } = new HardCodedText("Steam");

        public ILocalizedText Title { get; } = new HardCodedText(steamLocation.FolderName);
    }
}
