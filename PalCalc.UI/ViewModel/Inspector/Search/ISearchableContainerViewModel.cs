using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public abstract class ISearchableContainerViewModel : ObservableObject
    {
        public abstract string Id { get; }
        public abstract LocationType DetectedType { get; }

        public int PerRow => DetectedType switch
        {
            LocationType.PlayerParty => 5,
            LocationType.Palbox => 6,
            LocationType.Base => 5,
            _ => throw new NotImplementedException()
        };

        public abstract bool HasPages { get; }
        public abstract int RowsPerPage { get; }

        public abstract List<ContainerGridViewModel> Grids { get; }

        public ISearchCriteria SearchCriteria
        {
            set
            {
                foreach (var grid in Grids)
                    grid.SearchCriteria = value;
            }
        }

        public IContainerGridSlotViewModel SelectedSlot => Grids.FirstOrDefault(g => g.SelectedSlot != null)?.SelectedSlot;
        public ContainerGridPalSlotViewModel SelectedPalSlot => SelectedSlot as ContainerGridPalSlotViewModel;
    }
}
