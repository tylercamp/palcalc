using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Inspector.Search.Grid
{
    public partial class ContainerGridCustomPalSlotViewModel(CustomPalInstanceViewModel instance)
        : ObservableObject, IContainerGridInspectableSlotViewModel
    {
        public CustomPalInstanceViewModel PalInstance => instance;

        [ObservableProperty]
        private bool matches = true;
    }

    public class ContainerGridNewPalSlotViewModel : IContainerGridSlotViewModel
    {
        public bool Matches => false;
    }

    public partial class CustomContainerGridViewModel : ObservableObject, IContainerGridViewModel
    {
        private CustomContainerViewModel container;
        public CustomContainerGridViewModel(CustomContainerViewModel container)
        {
            this.container = container;
            Slots = new ObservableCollection<IContainerGridSlotViewModel>(
                container.Contents.Select(vm => new ContainerGridCustomPalSlotViewModel(vm))
            );

            Slots.Add(new ContainerGridNewPalSlotViewModel());

            container.Contents.CollectionChanged += Contents_CollectionChanged;
        }

        private void Contents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems.Count != 1) throw new NotImplementedException();

                    var newItem = (CustomPalInstanceViewModel)e.NewItems[0];
                    var newSlot = new ContainerGridCustomPalSlotViewModel(newItem);
                    Slots.Insert(e.NewStartingIndex, newSlot);

                    if (SelectedSlot is ContainerGridNewPalSlotViewModel)
                    {
                        SelectedSlot = newSlot;
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    throw new NotImplementedException();

                default:
                    throw new NotImplementedException();
            }
        }

        [ObservableProperty]
        private int perRow; 

        public ISearchCriteria SearchCriteria
        {
            set
            {
                foreach (var slot in Slots.OfType<ContainerGridCustomPalSlotViewModel>())
                    slot.Matches = slot.PalInstance.IsValid && value.Matches(slot.PalInstance.ModelObject);

                if (SelectedSlot != null && !SelectedSlot.Matches)
                    SelectedSlot = null;
            }
        }

        public Visibility GridVisibility => Visibility.Visible;

        public ILocalizedText Title => null;

        public Visibility TitleVisibility => Visibility.Collapsed;

        private IContainerGridSlotViewModel selectedSlot;
        public IContainerGridSlotViewModel SelectedSlot
        {
            get => selectedSlot;
            set
            {
                if (SetProperty(ref selectedSlot, value))
                {
                    if (value is ContainerGridNewPalSlotViewModel)
                    {
                        container.Contents.Add(
                            new CustomPalInstanceViewModel(container.Label)
                        );
                    }
                }
            }
        }

        public ObservableCollection<IContainerGridSlotViewModel> Slots { get; }
    }
}
