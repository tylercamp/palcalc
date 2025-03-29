using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Inspector.Search.Grid;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search.Container
{
    public partial class DefaultSearchableContainerViewModel(GameSettings settings, IPalContainer sourceContainer, List<PalInstance> contents) : ISearchableContainerViewModel(settings)
    {
        override public string Id => sourceContainer.Id;
        override public LocationType DetectedType => sourceContainer.Type;

        public IPalContainer SourceContainer => sourceContainer;
        
        public List<string> OwnerIds { get; } = contents.Select(p => p.OwnerPlayerId).Distinct().ToList();
        public List<PalInstance> RawContents => contents;

        public List<PalInstance> SlotContents { get; } =
            Enumerable.Range(0, contents.Max(p => p.Location.Index) + 1)
                .Select(i => contents.SingleOrDefault(p => p.Location.Index == i))
                .ToList();

        private List<IContainerGridViewModel> grids = null;
        public override List<IContainerGridViewModel> Grids
        {
            get
            {
                if (grids == null)
                {
                    var gridHeight = settings.LocationTypeGridHeights[DetectedType];
                    if (gridHeight == null)
                    {
                        grids = [new DefaultContainerGridViewModel(SlotContents) { RowSize = RowSize }];
                    }
                    else
                    {
                        var rowsPerPage = gridHeight.Value;
                        grids = SlotContents
                            .Batched(RowSize * rowsPerPage).ToList()
                            .ZipWithIndex()
                            .Select(pair => new DefaultContainerGridViewModel(pair.Item1.ToList()) {
                                Title = LocalizationCodes.LC_LOC_PALBOX_TAB.Bind(pair.Item2 + 1),
                                RowSize = RowSize
                            })
                            .Cast<IContainerGridViewModel>()
                            .ToList();
                    }

                    foreach (var grid in grids)
                    {
                        grid.PropertyChanged += OnSyncGridSelectedSlot;
                    }
                }

                return grids;
            }
        }
    }
}
