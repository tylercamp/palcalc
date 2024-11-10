using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public partial class DefaultSearchableContainerViewModel(string id, LocationType detectedType, List<PalInstance> contents) : ISearchableContainerViewModel
    {
        override public string Id => id;
        override public LocationType DetectedType => detectedType;
        
        public List<string> OwnerIds { get; } = contents.Select(p => p.OwnerPlayerId).Distinct().ToList();
        public List<PalInstance> RawContents => contents;

        public List<PalInstance> SlotContents { get; } =
            Enumerable.Range(0, contents.Max(p => p.Location.Index) + 1)
                .Select(i => contents.SingleOrDefault(p => p.Location.Index == i))
                .ToList();

        override public bool HasPages { get; } = detectedType == LocationType.Palbox;

        override public int RowsPerPage { get; } = 5;

        private List<ContainerGridViewModel> grids = null;
        public override List<ContainerGridViewModel> Grids
        {
            get
            {
                if (grids == null)
                {
                    if (!HasPages)
                    {
                        grids = [new ContainerGridViewModel(SlotContents) { PerRow = PerRow }];
                    }
                    else
                    {
                        grids = SlotContents
                            .Batched(PerRow * RowsPerPage).ToList()
                            .ZipWithIndex()
                            .Select(pair => new ContainerGridViewModel(pair.Item1.ToList()) {
                                Title = LocalizationCodes.LC_LOC_PALBOX_TAB.Bind(pair.Item2 + 1),
                                PerRow = PerRow
                            })
                            .ToList();
                    }

                    foreach (var grid in grids)
                    {
                        grid.PropertyChanged += Grid_PropertyChanged;
                    }
                }

                return grids;
            }
        }

        // if a value was selected in one grid, deselect values in all other grids
        private bool isSyncingSlots = false;
        private void Grid_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (isSyncingSlots || e.PropertyName != nameof(ContainerGridViewModel.SelectedSlot)) return;

            isSyncingSlots = true;
            var srcGrid = sender as ContainerGridViewModel;

            foreach (var grid in Grids)
            {
                if (grid == srcGrid)
                    continue;

                grid.SelectedSlot = null;
            }

            isSyncingSlots = false;

            OnPropertyChanged(nameof(SelectedSlot));
            OnPropertyChanged(nameof(SelectedPalSlot));
        }
    }
}
