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
        PalSourceViewModel sourcePals;

        public OwnedPalListPresetViewModel(PalSourceViewModel sourcePals) : base(LocalizationCodes.LC_PAL_LIST_PRESETS_BUILTIN_OWNED.Bind())
        {
            this.sourcePals = sourcePals;
        }

        public override List<Pal> Pals => sourcePals.AvailablePals.Select(p => p.Pal).Distinct().ToList();
    }
}
