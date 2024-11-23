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
            Label = value.Label;
            Contents = new ObservableCollection<CustomPalInstanceViewModel>(value.Contents.Select(i => new CustomPalInstanceViewModel(i)));
        }

        public CustomContainer ModelObject => new CustomContainer()
        {
            Label = Label,
            Contents = Contents.Select(p => p.ModelObject).SkipNull().ToList(),
        };

        [ObservableProperty]
        private string label;

        // (hide the detail that ID-based references should be to a custom container's label)
        public string ContainerId => Label;

        public ObservableCollection<CustomPalInstanceViewModel> Contents { get; }

        public LocationType LocationType { get; } = LocationType.Custom;
    }
}
