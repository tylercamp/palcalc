using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    internal partial class BreedingTreeNodeViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Traits))]
        private IBreedingTreeNode value = null;

        [ObservableProperty]
        private ObservableCollection<Trait> traits = new ObservableCollection<Trait>();


    }
}
