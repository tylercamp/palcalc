using CommunityToolkit.Mvvm.ComponentModel;
using GongSolutions.Wpf.DragDrop;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
            var job = item.LatestJob;

            if (job == null)
                throw new InvalidOperationException();

            if (!orderedPendingTargets.Any(t => t.LatestJob.CurrentState == SolverState.Running))
                job.Run();
            else
                job.Pause();
            
            orderedPendingTargets.Add(item);

            // TODO - event listener leaks
            job.JobStopped += () =>
            {
                orderedPendingTargets.Remove(item);
            };

            job.PropertyChanged += (_, ev) =>
            {
                if (ev.PropertyName == nameof(job.CurrentState) && job.CurrentState == SolverState.Running)
                {
                    var othersRunning = orderedPendingTargets.Where(t => t.LatestJob.CurrentState == SolverState.Running && t != item).ToList();
                    foreach (var other in othersRunning)
                        other.LatestJob.Pause();

                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        () => orderedPendingTargets.Move(orderedPendingTargets.IndexOf(item), 0)
                    );
                }
            };
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (!dropInfo.IsSameDragDropContextAsSource)
                return;

            var sourceItem = dropInfo.Data as PalSpecifierViewModel;
            var targetItem = dropInfo.TargetItem as PalSpecifierViewModel;

            if (
                sourceItem != null &&
                targetItem != null &&
                sourceItem != targetItem &&
                !targetItem.IsReadOnly &&
                QueuedItems.Contains(sourceItem)
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

            if (!QueuedItems.Contains(sourceItem) || !QueuedItems.Contains(targetItem)) return;

            var sourceIndex = QueuedItems.IndexOf(sourceItem);
            var targetIndex = QueuedItems.IndexOf(targetItem);

            int newIndex = dropInfo.InsertIndex;
            if (sourceIndex < targetIndex) newIndex -= 1;

            if (sourceIndex == newIndex) return;

            orderedPendingTargets.Move(QueuedItems.IndexOf(sourceItem), Math.Clamp(newIndex, 0, QueuedItems.Count - 1));
        }
    }
}
