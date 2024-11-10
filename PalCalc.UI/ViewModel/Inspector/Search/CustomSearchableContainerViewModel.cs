using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public class CustomSearchableContainerViewModel(CustomContainerViewModel container) : ISearchableContainerViewModel
    {
        public string Label => container.Label;

        public override string Id => container.Label;

        public override LocationType DetectedType => container.LocationType;

        private List<IContainerGridViewModel> grids;
        public override List<IContainerGridViewModel> Grids
        {
            get
            {
                // (not including Grid PropertyChanged logic from `DefaultSearchableContainerViewModel` since
                // this container will only ever have a single grid (for now), so we don't need to worry about
                // updating "other grids")
                if (grids == null)
                {
                    grids = [new CustomContainerGridViewModel(container) { PerRow = PerRow }];

                    foreach (var grid in grids)
                    {
                        grid.PropertyChanged += OnSyncGridSelectedSlot;
                    }
                }
                return grids;
            }
        }

        public override bool HasPages => false;

        public override int RowsPerPage { get; } = 5;
    }
}
