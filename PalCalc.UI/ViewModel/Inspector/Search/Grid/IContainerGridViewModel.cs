using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

namespace PalCalc.UI.ViewModel.Inspector.Search.Grid
{
    public interface IContainerGridSlotViewModel
    {
        /// <summary>
        /// Whether this slot matches the latest applied ISearchCriteria
        /// </summary>
        bool Matches { get; }

        /// <summary>
        /// Whether this slot should respond to clicks, usually related to `Matches`
        /// </summary>
        bool CanInterract { get; }
    }

    public interface IContainerGridPopulatedSlotViewModel : IContainerGridSlotViewModel
    {
    }

    public interface IContainerGridViewModel : INotifyPropertyChanged
    {
        public int PerRow { get; }

        public ISearchCriteria SearchCriteria { set; }

        public Visibility GridVisibility { get; }

        public ILocalizedText Title { get; }
        public Visibility TitleVisibility { get; }

        public IContainerGridSlotViewModel SelectedSlot { get; set; }

        public IRelayCommand<IContainerGridSlotViewModel> DeleteSlotCommand { get; }

        public ObservableCollection<IContainerGridSlotViewModel> Slots { get; }
    }
}
