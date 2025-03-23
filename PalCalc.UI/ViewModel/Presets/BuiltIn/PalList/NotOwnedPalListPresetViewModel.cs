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
    public class NotOwnedPalListPresetViewModel : BuiltInPalListPresetViewModel
    {
        private CachedSaveGame context;
        private IPalSource availablePalFilter;

        public NotOwnedPalListPresetViewModel(CachedSaveGame context, IPalSource availablePalFilter) : base(LocalizationCodes.LC_PAL_LIST_PRESETS_BUILTIN_NOT_OWNED.Bind())
        {
            this.context = context;
            this.availablePalFilter = availablePalFilter;
        }

        public override List<Pal> Pals =>
            availablePalFilter == null
                ? []
                : PalDB.LoadEmbedded().Pals
                    .Except(availablePalFilter.Filter(context).Select(p => p.Pal))
                    .Distinct()
                    .ToList();
    }
}
