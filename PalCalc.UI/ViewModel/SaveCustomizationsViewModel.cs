using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.PalDerived;
using PalCalc.UI.ViewModel.SaveSelection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel
{
    class Debouncer : IDisposable
    {
        private DispatcherTimer timer;
        private readonly Action action;

        public Debouncer(TimeSpan delay, Action action, Dispatcher dispatcher)
        {
            this.action = action;
            timer = new(
                delay,
                DispatcherPriority.Normal,
                (_, _) =>
                {
                    action();
                    timer.Stop();
                },
                dispatcher
            );
        }

        public void Run()
        {
            if (timer == null) return;

            if (timer.IsEnabled)
                timer.Stop();

            timer.Start();
        }

        public void Dispose()
        {
            Cancel();
            timer = null;
        }

        public void Flush()
        {
            if (timer == null || !timer.IsEnabled) return;

            timer.Stop();
            action();
        }

        public void Cancel() => timer?.Stop();
    }

    // (Auto-saves any changes with debounce. There should only ever be one instance for each save file)
    public partial class SaveCustomizationsViewModel : ObservableObject, IDisposable
    {
        // TODO - Move this application-lifetime cache into a save/session service during the broader refactor.
        private static readonly object instancesLock = new();
        private static readonly Dictionary<SaveIdentity, SaveCustomizationsViewModel> instances = new();

        public static SaveCustomizationsViewModel GetOrCreate(ISaveGame save)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                return dispatcher.Invoke(() => GetOrCreate(save));

            var identity = SaveIdentity.From(save);
            lock (instancesLock)
            {
                if (!instances.TryGetValue(identity, out var result))
                {
                    result = new SaveCustomizationsViewModel(save, dispatcher ?? Dispatcher.CurrentDispatcher);
                    instances.Add(identity, result);
                }

                return result;
            }
        }

        public static void RemoveFor(ISaveGame save)
        {
            SaveCustomizationsViewModel result = null;
            lock (instancesLock)
            {
                var identity = SaveIdentity.From(save);
                if (instances.TryGetValue(identity, out result))
                    instances.Remove(identity);
            }

            result?.Dispose();
        }

        public static void FlushAll()
        {
            List<SaveCustomizationsViewModel> current;
            lock (instancesLock)
                current = [.. instances.Values];

            foreach (var customizations in current)
                customizations.Flush();
        }

        private Debouncer saveAction;

        private SaveCustomizationsViewModel(ISaveGame save, Dispatcher dispatcher)
        {
            var data = Storage.LoadSaveCustomizations(save, PalDB.LoadEmbedded());

            CustomContainers = new ObservableCollection<CustomContainerViewModel>(data.CustomContainers.Select(c => new CustomContainerViewModel(c)));

            foreach (var container in CustomContainers)
                StartMonitorContainer(container);

            CustomContainers.CollectionChanged += CustomContainers_CollectionChanged;

            saveAction = new Debouncer(TimeSpan.FromSeconds(3), () =>
            {
                Storage.SaveCustomizations(save, ModelObject, PalDB.LoadEmbedded());
            }, dispatcher);
        }

        public void Dispose()
        {
            saveAction.Dispose();
        }

        public void Flush() => saveAction.Flush();

        public SaveCustomizations ModelObject => new SaveCustomizations()
        {
            CustomContainers = CustomContainers.Select(c => c.ModelObject).ToList()
        };

        public ObservableCollection<CustomContainerViewModel> CustomContainers { get; }

        private void StartMonitorContainer(CustomContainerViewModel container)
        {
            container.PropertyChanged += Container_PropertyChanged;
            container.Contents.CollectionChanged += ContainerContents_CollectionChanged;

            foreach (var inst in container.Contents)
                inst.PropertyChanged += PalInst_PropertyChanged;
        }

        private void StopMonitorContainer(CustomContainerViewModel container)
        {
            container.PropertyChanged -= Container_PropertyChanged;
            container.Contents.CollectionChanged -= ContainerContents_CollectionChanged;

            foreach (var inst in container.Contents)
                inst.PropertyChanged -= PalInst_PropertyChanged;
        }

        private void PalInst_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            saveAction.Run();
            OnPropertyChanged(nameof(CustomContainers));
        }

        private void Container_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            saveAction.Run();
            OnPropertyChanged(nameof(CustomContainers));
        }

        private void ContainerContents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var p in AddedItems<CustomPalInstanceViewModel>(e))
                p.PropertyChanged += PalInst_PropertyChanged;

            foreach (var p in RemovedItems<CustomPalInstanceViewModel>(e))
                p.PropertyChanged -= PalInst_PropertyChanged;

            saveAction.Run();
            OnPropertyChanged(nameof(CustomContainers));
        }

        private void CustomContainers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var c in AddedItems<CustomContainerViewModel>(e))
                StartMonitorContainer(c);

            foreach (var c in RemovedItems<CustomContainerViewModel>(e))
                StopMonitorContainer(c);

            saveAction.Run();
            OnPropertyChanged(nameof(CustomContainers));
        }

        private IEnumerable<T> AddedItems<T>(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                    return e.NewItems.Cast<T>();

                default: return [];
            }
        }

        private IEnumerable<T> RemovedItems<T>(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Replace:
                    return e.OldItems.Cast<T>();

                default: return [];
            }
        }
    }
}
