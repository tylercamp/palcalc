using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.ApplicationModel.Contacts;

namespace PalCalc.UI.ViewModel.Solver
{
    partial class SolverQueueViewModel : ObservableObject, IDropTarget
    {
        private ObservableCollection<PalSpecifierViewModel> orderedPendingTargets;
        public ReadOnlyObservableCollection<PalSpecifierViewModel> QueuedItems { get; }

        // (jobs may be cleared from a PalSpecifierViewModel when they're cancelled, track them here)
        private Dictionary<PalSpecifierViewModel, SolverJobViewModel> itemJobs = new();

        [ObservableProperty]
        private IRelayCommand<PalSpecifierViewModel> selectItemCommand;

        // TODO - is this used?
        [ObservableProperty]
        private bool paused = false;

        public SolverQueueViewModel()
        {
            orderedPendingTargets = new ObservableCollection<PalSpecifierViewModel>();
            QueuedItems = new ReadOnlyObservableCollection<PalSpecifierViewModel>(orderedPendingTargets);

            orderedPendingTargets.CollectionChanged += OrderedPendingTargets_CollectionChanged;
        }

        private void OrderedPendingTargets_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var vm in orderedPendingTargets.Where(t => !itemJobs.ContainsKey(t)))
                itemJobs.Add(vm, vm.LatestJob);

            // note: events are raised by SolverJobViewModel *before* job state is changed
            var wasRunning = orderedPendingTargets
                .Concat(e.NewItems?.Cast<PalSpecifierViewModel>() ?? [])
                .Concat(e.OldItems?.Cast<PalSpecifierViewModel>() ?? [])
                .Any(t => itemJobs.GetValueOrDefault(t)?.CurrentState == SolverState.Running);

            var runningTargets = orderedPendingTargets.Where(t => itemJobs[t].CurrentState == SolverState.Running).ToList();
            var firstItem = orderedPendingTargets.FirstOrDefault();

            void RunFirstInQueue()
            {
                foreach (var job in runningTargets.Where(j => j != firstItem && itemJobs[j].CurrentState == SolverState.Running))
                    itemJobs[job].Pause();

                itemJobs[firstItem].Run();
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    RunFirstInQueue();
                    break;

                case NotifyCollectionChangedAction.Reset:
                    foreach (var target in runningTargets)
                        itemJobs[target].Cancel();
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (wasRunning && orderedPendingTargets.Count > 0)
                        RunFirstInQueue();
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldStartingIndex == 0 || e.NewStartingIndex == 0 && wasRunning)
                        RunFirstInQueue();
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.OldStartingIndex == 0 || e.NewStartingIndex == 0 && wasRunning)
                        RunFirstInQueue();
                    break;
            }

            foreach (var key in itemJobs.Keys.Where(k => !orderedPendingTargets.Contains(k)).ToList())
                itemJobs.Remove(key);
        }

        public void Run(PalSpecifierViewModel item)
        {
            var job = item.LatestJob;

            if (job == null)
                throw new InvalidOperationException();

            // TODO - event listener leaks
            job.JobStopped += () =>
            {
                orderedPendingTargets.Remove(item);
            };

            job.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(job.CurrentState))
                    return;

                if (job.CurrentState != SolverState.Running)
                    return;

                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    if (!orderedPendingTargets.Contains(item))
                        return;

                    if (job.CurrentState == SolverState.Running && orderedPendingTargets[0] != item)
                        orderedPendingTargets.Move(orderedPendingTargets.IndexOf(item), 0);
                });
            };

            itemJobs.Add(item, item.LatestJob);
            orderedPendingTargets.Insert(0, item);
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
