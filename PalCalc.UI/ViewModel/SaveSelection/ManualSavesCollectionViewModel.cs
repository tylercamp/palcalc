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
    internal class ManualSavesCollectionViewModel(ManualSavesLocationViewModel location) : ISavesCollectionViewModel
    {
        public IReadOnlyCollection<SaveGameViewModel> AvailableSaves { get; } =
            new ReadOnlyCollection<SaveGameViewModel>([.. location.SaveGames.OfType<SaveGameViewModel>()]);

        // TODO ITL
        public ILocalizedText TypeLabel { get; } = new HardCodedText("Local Files");

        public ILocalizedText Title { get; } = null;
    }
}
