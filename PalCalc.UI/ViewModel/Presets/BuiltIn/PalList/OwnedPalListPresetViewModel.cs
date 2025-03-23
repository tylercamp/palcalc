using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Presets.BuiltIn.PalList
{
    public class OwnedPalListPresetViewModel : BuiltInPalListPresetViewModel
    {
        CachedSaveGame context;
        IPalSource availablePalFilter;

        public OwnedPalListPresetViewModel(CachedSaveGame context, IPalSource availablePalFilter) : base(LocalizationCodes.LC_PAL_LIST_PRESETS_BUILTIN_OWNED.Bind())
        {
            this.context = context;
            this.availablePalFilter = availablePalFilter;
        }

        public override List<Pal> Pals =>
            availablePalFilter == null
                ? []
                : availablePalFilter.Filter(context).Select(p => p.Pal).Distinct().ToList();
    }
}
