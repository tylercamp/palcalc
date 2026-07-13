using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal interface ISavesCollectionViewModel
    {
        IReadOnlyCollection<SaveGameViewModel> AvailableSaves { get; }

        ILocalizedText TypeLabel { get; }

        ILocalizedText Title { get; }
    }
}
