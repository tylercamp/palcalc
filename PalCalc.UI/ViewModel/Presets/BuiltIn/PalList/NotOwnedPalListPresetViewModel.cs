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
        private PalSourceViewModel sourcePals;

        public NotOwnedPalListPresetViewModel(PalSourceViewModel sourcePals) : base(LocalizationCodes.LC_PAL_LIST_PRESETS_BUILTIN_NOT_OWNED.Bind())
        {
            this.sourcePals = sourcePals;
        }

        public override List<Pal> Pals =>
            PalDB.LoadEmbedded().Pals
                .Except(sourcePals.AvailablePals.Select(p => p.Pal).Distinct().ToList())
                .Distinct()
                .ToList();
    }
}
