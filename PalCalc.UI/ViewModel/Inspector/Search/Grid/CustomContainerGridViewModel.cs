using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.PalDerived;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Inspector.Search.Grid
{
    public partial class ContainerGridCustomPalSlotViewModel
        : ObservableObject, IContainerGridPopulatedSlotViewModel
    {
        public ContainerGridCustomPalSlotViewModel(CustomPalInstanceViewModel instance)
        {
            PalInstance = instance;

            PropertyChangedEventManager.AddHandler(
                instance,
                (_, _) => OnPropertyChanged(nameof(CanInterract)),
                nameof(instance.IsValid)
            );
        }

        public CustomPalInstanceViewModel PalInstance { get; }

        // note: changes to `PalInstance` will not auto-update `matches` since we don't preserve
        //       the applied filter anywhere in these VMs. very minor issue, won't bother with a fix
        [NotifyPropertyChangedFor(nameof(CanInterract))]
        [ObservableProperty]
        private bool matches = true;

        public bool CanInterract => !PalInstance.IsValid || Matches;
    }

    public class ContainerGridNewPalSlotViewModel : IContainerGridSlotViewModel
    {
        public bool Matches => false;
        public bool CanInterract => true;
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

            DeleteSlotCommand = new RelayCommand<IContainerGridSlotViewModel>(
                execute: item => container.Contents.Remove((item as ContainerGridCustomPalSlotViewModel)?.PalInstance),
                canExecute: item => item is ContainerGridCustomPalSlotViewModel && Slots.Contains(item)
            );
        }

        [ObservableProperty]
        private int rowSize;

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

        private IContainerGridSlotViewModel selectedSlot;
        public IContainerGridSlotViewModel SelectedSlot
        {
            get => selectedSlot;
            set
            {
                if (SetProperty(ref selectedSlot, value) && value is ContainerGridNewPalSlotViewModel)
                    container.Contents.Add(new CustomPalInstanceViewModel(container));
            }
        }

        // changes to contents of the main container VM are reflected here via property events
        // this `Slots` should _not_ be modified directly
        public ObservableCollection<IContainerGridSlotViewModel> Slots { get; }

        public IRelayCommand<IContainerGridSlotViewModel> DeleteSlotCommand { get; }

        private void Contents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems.Count != 1) throw new NotImplementedException();

                    var newSlot = new ContainerGridCustomPalSlotViewModel(
                        (CustomPalInstanceViewModel)e.NewItems[0]
                    );

                    Slots.Insert(e.NewStartingIndex, newSlot);

                    if (SelectedSlot is ContainerGridNewPalSlotViewModel)
                        SelectedSlot = newSlot;

                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems.Count != 1) throw new NotImplementedException();

                    Slots.RemoveAt(e.OldStartingIndex);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
