using PalCalc.Model;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class PalInstanceViewModel(PalInstance inst)
    {
        public PalInstance ModelObject => inst;

        public PalViewModel Pal { get; } = PalViewModel.Instance[inst.Pal];

        public TraitCollectionViewModel Traits { get; } = new TraitCollectionViewModel(inst.Traits.Select(TraitViewModel.Make));

        public ILocalizedText Gender { get; } = inst.Gender.Label();
    }
}
