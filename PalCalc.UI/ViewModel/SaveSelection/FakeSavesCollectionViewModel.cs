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
    internal class FakeSavesCollectionViewModel(IEnumerable<ISaveGame> fakeSaves) : ISavesCollectionViewModel
    {
        public IReadOnlyCollection<SaveGameViewModel> AvailableSaves { get; } =
            new ReadOnlyCollection<SaveGameViewModel>([.. fakeSaves.Select(fs => new SaveGameViewModel(null, fs))]);

        // TODO ITL
        public ILocalizedText TypeLabel { get; } = new HardCodedText("Fake Saves");

        public ILocalizedText Title { get; } = null;
    }
}
