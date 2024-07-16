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
    public class ContainerViewModel(string id, LocationType detectedType, List<PalInstance> contents) : ObservableObject
    {
        public string Id => id;
        public LocationType DetectedType => detectedType;
        public List<string> OwnerIds { get; } = contents.Select(p => p.OwnerPlayerId).Distinct().ToList();
        public List<PalInstance> Contents => contents;

        public List<PalInstance> SlotContents { get; } =
            Enumerable.Range(0, contents.Max(p => p.Location.Index))
                .Select(i => contents.SingleOrDefault(p => p.Location.Index == i))
                .ToList();

        public bool HasPages { get; } = detectedType == LocationType.Palbox;

        public int PerRow { get; } = detectedType switch
        {
            LocationType.PlayerParty => 5,
            LocationType.Palbox => 6,
            LocationType.Base => 5,
            _ => throw new NotImplementedException()
        };

        public int RowsPerPage { get; } = 5;

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

        private List<ContainerGridViewModel> grids = null;
        public List<ContainerGridViewModel> Grids
        {
            get
            {
                if (grids == null)
                {
                    if (!HasPages)
                    {
                        grids = [new ContainerGridViewModel(Contents) { PerRow = PerRow }];
                    }
                    else
                    {
                        grids = Contents
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
