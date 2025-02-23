using CommunityToolkit.Mvvm.ComponentModel;
using GongSolutions.Wpf.DragDrop;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    class SolverQueueViewModel : ObservableObject, IDropTarget
    {
        private ObservableCollection<PalSpecifierViewModel> orderedPendingTargets;
        public ReadOnlyObservableCollection<PalSpecifierViewModel> QueuedItems { get; }

        public SolverQueueViewModel()
        {
            orderedPendingTargets = new ObservableCollection<PalSpecifierViewModel>();
            QueuedItems = new ReadOnlyObservableCollection<PalSpecifierViewModel>(orderedPendingTargets);

            orderedPendingTargets.CollectionChanged += OrderedPendingTargets_CollectionChanged;
        }

        private void OrderedPendingTargets_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (orderedPendingTargets.Any(t => t.LatestJob.CurrentState == SolverState.Running))
                return;

            if (orderedPendingTargets.Count == 0)
                return;

            var nextItem = orderedPendingTargets.First();
            nextItem.LatestJob.Run();
        }

        public void Add(PalSpecifierViewModel item)
        {
            if (item.LatestJob == null)
                throw new InvalidOperationException();

            if (!orderedPendingTargets.Any(t => t.LatestJob.CurrentState == SolverState.Running))
                item.LatestJob.Run();
            else
                item.LatestJob.Pause();
            
            orderedPendingTargets.Add(item);

            item.LatestJob.JobStopped += () =>
            {
                orderedPendingTargets.Remove(item);
            };
        }

        public void DragOver(IDropInfo dropInfo)
        {
            // TODO
            throw new NotImplementedException();
        }

        public void Drop(IDropInfo dropInfo)
        {
            // TODO
            throw new NotImplementedException();
        }
    }
}
