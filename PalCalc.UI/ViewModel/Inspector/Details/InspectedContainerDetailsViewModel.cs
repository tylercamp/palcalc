using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Support.Level;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Details
{
    public partial class InspectedContainerDetailsViewModel(
        string groupName,
        OwnerViewModel owner,
        List<PalInstance> containedPals,
        List<GvasCharacterInstance> containedRawPals,
        RawPalContainerContents rawContainer,
        LocationType? locationType,
        WorldCoord position
    ) : ObservableObject
    {
        public List<IContainerSlotDetailsViewModel> Slots { get; } = Enumerable.Range(0, rawContainer.MaxEntries)
            .Select(i => rawContainer.Slots.SingleOrDefault(s => s.SlotIndex == i))
            .Select<PalContainerSlot, IContainerSlotDetailsViewModel>(s =>
            {
                if (s?.PalInstanceId is not { } id) return new EmptyPalContainerSlotDetailsViewModel();

                // (detect and explicitly ignore pals with duplicate IDs, we can't make any guarantees about
                // app behavior when there are duplicates. these are expected to be 100% unique within a save)
                var rawMatches = containedRawPals.Where(p => p.PalInstanceId == id).Take(2).ToArray();
                var rawChar = rawMatches.Length == 1 ? rawMatches[0] : null;
                var rawPal = containedPals.FirstOrDefault(p => p.PalInstanceId == id && p.Location.Index == s.SlotIndex);

                return new PalContainerSlotDetailsViewModel(id, rawPal, rawChar);
            })
            .ToList();

        public string DisplayGroupName => groupName;

        public int TotalSlots => rawContainer.MaxEntries;
        public int UsedSlots => rawContainer.NumEntries;

        public string Id => rawContainer.Id;
        public OwnerViewModel Owner => owner;
        public ILocalizedText Type { get; } = locationType?.FullLabel() ?? LocalizationCodes.LC_COMMON_UNKNOWN.Bind();

        public MapCoordViewModel Coord { get; } = MapCoordViewModel.FromCoord(position);
    }
}
