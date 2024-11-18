using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel
{
    class Debouncer : IDisposable
    {
        private DispatcherTimer timer;

        public Debouncer(TimeSpan delay, Action action)
        {
            timer = new(
                delay,
                DispatcherPriority.Normal,
                (_, _) =>
                {
                    action();
                    timer.Stop();
                },
                Dispatcher.CurrentDispatcher
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
            timer.Stop();
            timer = null;
        }
    }

    // (Auto-saves any changes with debounce. There should only ever be one instance for each save file)
    public partial class SaveCustomizationsViewModel : IDisposable
    {
        private Debouncer saveAction;

        public SaveCustomizationsViewModel(SaveGameViewModel save)
        {
            var data = Storage.LoadSaveCustomizations(save.Value, PalDB.LoadEmbedded());

            CustomContainers = new ObservableCollection<CustomContainerViewModel>(data.CustomContainers.Select(c => new CustomContainerViewModel(c)));

            foreach (var container in CustomContainers)
                StartMonitorContainer(container);

            CustomContainers.CollectionChanged += CustomContainers_CollectionChanged;

            saveAction = new Debouncer(TimeSpan.FromSeconds(3), () =>
            {
                Storage.SaveCustomizations(save.Value, ModelObject, PalDB.LoadEmbedded());
            });
        }

        public void Dispose()
        {
            saveAction.Dispose();
        }

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
            if ((sender as CustomPalInstanceViewModel).IsValid)
                saveAction.Run();
        }

        private void Container_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            saveAction.Run();
        }

        private void ContainerContents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var p in AddedItems<CustomPalInstanceViewModel>(e))
                p.PropertyChanged += PalInst_PropertyChanged;

            foreach (var p in RemovedItems<CustomPalInstanceViewModel>(e))
                p.PropertyChanged -= PalInst_PropertyChanged;

            saveAction.Run();
        }

        private void CustomContainers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var c in AddedItems<CustomContainerViewModel>(e))
                StartMonitorContainer(c);

            foreach (var c in RemovedItems<CustomContainerViewModel>(e))
                StopMonitorContainer(c);

            saveAction.Run();
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
