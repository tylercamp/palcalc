using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public class ContainerViewModel(string id, LocationType detectedType, List<PalInstance> contents)
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

        public List<ContainerGridViewModel> Grids
        {
            get
            {
                if (!HasPages)
                {
                    return [new ContainerGridViewModel() { PerRow = PerRow, Contents = Contents }];
                }
                else
                {
                    var pages = Contents.Batched(PerRow * RowsPerPage).ToList();

                    return pages.Zip(Enumerable.Range(1, pages.Count))
                        .Select(pair => new ContainerGridViewModel() { Title = $"Page {pair.Second}", Contents = pair.First.ToList(), PerRow = PerRow })
                        .ToList();
                }
            }
        }
    }
}
