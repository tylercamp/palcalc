using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    public partial class BreedingTreeNodeViewModel : ObservableObject
    {
        private PalDB db;
        public BreedingTreeNodeViewModel(IBreedingTreeNode node)
        {
            Value = node;
            Pal = new PalViewModel(node.PalRef.Pal);
        }

        public PalViewModel Pal { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Traits))]
        private IBreedingTreeNode value = null;

        [ObservableProperty]
        private ObservableCollection<TraitViewModel> traits = new ObservableCollection<TraitViewModel>();
    }
}
