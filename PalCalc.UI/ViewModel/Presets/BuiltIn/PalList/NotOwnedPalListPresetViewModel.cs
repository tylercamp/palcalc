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
        private List<IPalSourceTreeSelection> selections;

        public NotOwnedPalListPresetViewModel(CachedSaveGame context, List<IPalSourceTreeSelection> selections) : base(LocalizationCodes.LC_PAL_LIST_PRESETS_BUILTIN_NOT_OWNED.Bind())
        {
            this.context = context;
            this.selections = selections;
        }

        public override List<Pal> Pals =>
            selections == null
                ? []
                : PalDB.LoadEmbedded().Pals
                    .Except(context.OwnedPals.Where(p => selections.Any(s => s.Matches(context, p))).Select(p => p.Pal))
                    .Distinct()
                    .ToList();
    }
}
