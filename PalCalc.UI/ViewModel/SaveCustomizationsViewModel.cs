using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public class SaveCustomizationsViewModel
    {
        private SaveGameViewModel save;

        private void SaveChanges()
        {
            Storage.SaveCustomizations(save.Value, ModelObject, PalDB.LoadEmbedded());
        }

        public SaveCustomizationsViewModel(SaveGameViewModel save)
        {
            this.save = save;
            var data = Storage.LoadSaveCustomizations(save.Value, PalDB.LoadEmbedded());

            CustomContainers = new ObservableCollection<CustomContainerViewModel>(data.CustomContainers.Select(c => new CustomContainerViewModel(c)));

            foreach (var container in CustomContainers)
                StartMonitorContainer(container);

            CustomContainers.CollectionChanged += CustomContainers_CollectionChanged;
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
                SaveChanges();
        }

        private void ContainerContents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var p in AddedItems<CustomPalInstanceViewModel>(e))
                p.PropertyChanged += PalInst_PropertyChanged;

            foreach (var p in RemovedItems<CustomPalInstanceViewModel>(e))
                p.PropertyChanged -= PalInst_PropertyChanged;

            SaveChanges();
        }

        private void Container_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SaveChanges();
        }

        private void CustomContainers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var c in AddedItems<CustomContainerViewModel>(e))
                StartMonitorContainer(c);

            foreach (var c in RemovedItems<CustomContainerViewModel>(e))
                StopMonitorContainer(c);

            SaveChanges();
        }

        private IEnumerable<T> AddedItems<T>(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add |
                     NotifyCollectionChangedAction.Replace:
                    return e.NewItems.Cast<T>();

                default: return [];
            }
        }

        private IEnumerable<T> RemovedItems<T>(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove |
                     NotifyCollectionChangedAction.Reset |
                     NotifyCollectionChangedAction.Replace:
                    return e.OldItems.Cast<T>();

                default: return [];
            }
        }
    }
}
