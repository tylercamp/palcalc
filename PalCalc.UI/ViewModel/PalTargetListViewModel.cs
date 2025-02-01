using CommunityToolkit.Mvvm.ComponentModel;
using GongSolutions.Wpf.DragDrop;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{

    public partial class PalTargetListViewModel : ObservableObject, IDropTarget
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

        public event Action<PalTargetListViewModel> OrderChanged;

        public void Add(PalSpecifierViewModel value) => targets.Insert(1, value);
        public void Remove(PalSpecifierViewModel value) => targets.Remove(value);

        public void Replace(PalSpecifierViewModel oldValue, PalSpecifierViewModel newValue)
        {
            var oldIndex = targets.IndexOf(oldValue);
            targets[oldIndex] = newValue;
        }

        public void UpdateCachedData(CachedSaveGame csg, GameSettings settings)
        {
            foreach (var target in targets)
                target.CurrentResults?.UpdateCachedData(csg, settings);
        }

        public void DragOver(IDropInfo dropInfo)
        {
            var sourceItem = dropInfo.Data as PalSpecifierViewModel;
            var targetItem = dropInfo.TargetItem as PalSpecifierViewModel;

            if (
                sourceItem != null &&
                targetItem != null &&
                sourceItem != targetItem &&
                !targetItem.IsReadOnly &&
                Targets.Contains(sourceItem) &&
                dropInfo.InsertIndex > 0
            )
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var sourceItem = dropInfo.Data as PalSpecifierViewModel;
            var targetItem = dropInfo.TargetItem as PalSpecifierViewModel;

            var sourceIndex = targets.IndexOf(sourceItem);
            var targetIndex = targets.IndexOf(targetItem);

            int newIndex = dropInfo.InsertIndex;
            if (sourceIndex < targetIndex) newIndex -= 1;

            if (sourceIndex == newIndex) return;

            targets.Move(targets.IndexOf(sourceItem), Math.Clamp(newIndex, 1, targets.Count - 1));
            OrderChanged?.Invoke(this);
        }
    }
}
