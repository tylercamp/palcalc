using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{

    public partial class PalTargetListViewModel : ObservableObject
    {
        public PalTargetListViewModel()
        {
            targets = new ObservableCollection<PalSpecifierViewModel>
            {
                PalSpecifierViewModel.New
            };

            Targets = new ReadOnlyObservableCollection<PalSpecifierViewModel>(targets);
        }

        public PalTargetListViewModel(IEnumerable<PalSpecifierViewModel> existingSpecs)
        {
            targets = new ObservableCollection<PalSpecifierViewModel>()
            {
                PalSpecifierViewModel.New
            };

            foreach (var spec in existingSpecs)
                targets.Add(spec);

            Targets = new ReadOnlyObservableCollection<PalSpecifierViewModel>(targets);
        }

        private ObservableCollection<PalSpecifierViewModel> targets;
        public ReadOnlyObservableCollection<PalSpecifierViewModel> Targets { get; }

        public PalSpecifierViewModel SelectedTarget { get; set; } = null;

        public void Add(PalSpecifierViewModel value) => targets.Insert(1, value);
    }
}
