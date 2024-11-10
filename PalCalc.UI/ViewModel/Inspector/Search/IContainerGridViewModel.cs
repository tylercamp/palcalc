using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public interface IContainerGridSlotViewModel
    {
        bool Matches { get; }
    }

    public partial class ContainerGridPalSlotViewModel : ObservableObject, IContainerGridSlotViewModel
    {
        public PalInstanceViewModel PalInstance { get; set; }

        public PalViewModel Pal => PalInstance.Pal;

        [ObservableProperty]
        private bool matches = true;
    }

    public class ContainerGridEmptySlotViewModel : IContainerGridSlotViewModel
    {
        public bool Matches => false;
    }

    public interface IContainerGridViewModel : INotifyPropertyChanged
    {
        public int PerRow { get; }

        public ISearchCriteria SearchCriteria { set; }

        public Visibility GridVisibility { get; }

        public ILocalizedText Title { get; }
        public Visibility TitleVisibility { get; }

        public IContainerGridSlotViewModel SelectedSlot { get; set; }

        public ObservableCollection<IContainerGridSlotViewModel> Slots { get; }
    }
}
