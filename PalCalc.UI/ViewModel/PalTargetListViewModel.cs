using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.ViewModel.Mapped;
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
            SelectedTarget = PalSpecifierViewModel.New;
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
            SelectedTarget = PalSpecifierViewModel.New;
        }

        private ObservableCollection<PalSpecifierViewModel> targets;
        public ReadOnlyObservableCollection<PalSpecifierViewModel> Targets { get; }

        [ObservableProperty]
        private PalSpecifierViewModel selectedTarget;

        public void Add(PalSpecifierViewModel value) => targets.Insert(1, value);
        public void Remove(PalSpecifierViewModel value) => targets.Remove(value);

        public void Replace(PalSpecifierViewModel oldValue, PalSpecifierViewModel newValue)
        {
            var oldIndex = targets.IndexOf(oldValue);
            targets[oldIndex] = newValue;
        }
    }
}
