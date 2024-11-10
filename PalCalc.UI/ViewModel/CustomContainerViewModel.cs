using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public partial class CustomContainerViewModel : ObservableObject
    {
        public CustomContainerViewModel(CustomContainer value)
        {
            ModelObject = value;

            Label = value.Label;
            Contents = new ObservableCollection<CustomPalInstanceViewModel>(value.Contents.Select(i => new CustomPalInstanceViewModel(i)));

            Contents.CollectionChanged += Contents_CollectionChanged;
        }

        private void Contents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Contents));
        }

        public CustomContainer ModelObject { get; private set; }

        [ObservableProperty]
        private string label;

        public ObservableCollection<CustomPalInstanceViewModel> Contents { get; }

        public LocationType LocationType { get; } = LocationType.Palbox;
    }
}
