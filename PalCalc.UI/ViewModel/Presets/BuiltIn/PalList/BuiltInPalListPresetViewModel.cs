using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View.Utils;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Presets.BuiltIn.PalList
{
    public abstract class BuiltInPalListPresetViewModel : IPalListPresetViewModel, IFixedListItem
    {
        public BuiltInPalListPresetViewModel(ILocalizedText localizedLabel)
        {
            LocalizedLabel = localizedLabel;
            Name = LocalizedLabel.Value;

            PropertyChangedEventManager.AddHandler(
                localizedLabel,
                (_, _) => Name = LocalizedLabel.Value,
                nameof(localizedLabel.Value)
            );
        }

        public ILocalizedText LocalizedLabel { get; }
        public string Name { get; private set; }

        public abstract List<Pal> Pals { get; }

        public static List<BuiltInPalListPresetViewModel> BuildAll(CachedSaveGame context, IPalSource availablePalFilter) =>
        [
            new OwnedPalListPresetViewModel(context, availablePalFilter),
            new NotOwnedPalListPresetViewModel(context, availablePalFilter),
        ];
    }
}
