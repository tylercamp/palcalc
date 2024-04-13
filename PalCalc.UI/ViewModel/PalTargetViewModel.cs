using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public partial class PalTargetViewModel : ObservableObject
    {
        public PalTargetViewModel() : this(PalSpecifierViewModel.New) { }

        public PalTargetViewModel(PalSpecifierViewModel initial)
        {
            if (initial.IsReadOnly)
            {
                InitialPalSpecifier = null;
                CurrentPalSpecifier = new PalSpecifierViewModel();
            }
            else
            {
                InitialPalSpecifier = initial;
                CurrentPalSpecifier = initial.Copy();
            }
        }

        [ObservableProperty]
        private PalSpecifierViewModel initialPalSpecifier;

        [ObservableProperty]
        private PalSpecifierViewModel currentPalSpecifier;

        public List<PalViewModel> AvailablePals => AllPals;
        public List<TraitViewModel> AvailableTraits => AllTraits;

        public static List<PalViewModel> AllPals = PalDB.LoadEmbedded().Pals
            .OrderBy(p => p.Name)
            .Select(p => new PalViewModel(p))
            .ToList();

        public static List<TraitViewModel> AllTraits = PalDB.LoadEmbedded().Traits
            .OrderBy(t => t.Name)
            .DistinctBy(t => t.Name)
            .Select(t => new TraitViewModel(t))
            .ToList();
    }
}
