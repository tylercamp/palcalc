﻿using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.PalDerived
{
    public interface IPalLocationPreviewViewModel
    {
    }

    public class MapLocationPreviewViewModel(MapCoordViewModel coords) : IPalLocationPreviewViewModel
    {
        public static MapLocationPreviewViewModel DesignerInstance { get; } =
            new MapLocationPreviewViewModel(new MapCoordViewModel(new WorldCoord() { X = 0, Y = 0, Z = 0 }));

        public MapCoordViewModel MapCoord => coords;
    }

    public interface IContainerLocationPreviewSlotViewModel { }
    public class EmptyContainerLocationPreviewSlotViewModel : IContainerLocationPreviewSlotViewModel { }
    public class FocusedContainerLocationPreviewSlotViewModel : IContainerLocationPreviewSlotViewModel { }

    public class ContainerLocationPreviewViewModel(GameSettings settings, PalLocation location) : IPalLocationPreviewViewModel
    {
        public static ContainerLocationPreviewViewModel PartyDesignerInstance { get; } =
            new ContainerLocationPreviewViewModel(GameSettings.Defaults, new PalLocation() { Type = LocationType.PlayerParty, Index = 3 });

        public static ContainerLocationPreviewViewModel PalboxDesignerInstance { get; } =
            new ContainerLocationPreviewViewModel(GameSettings.Defaults, new PalLocation() { Type = LocationType.Palbox, Index = 13 });

        public static ContainerLocationPreviewViewModel BaseDesignerInstance { get; } =
            new ContainerLocationPreviewViewModel(GameSettings.Defaults, new PalLocation() { Type = LocationType.Base, Index = 9 });

        public static ContainerLocationPreviewViewModel CageDesignerInstance { get; } =
            new ContainerLocationPreviewViewModel(GameSettings.Defaults, new PalLocation() { Type = LocationType.ViewingCage, Index = 7 });

        public PalDisplayCoord ContainerCoord { get; } = PalDisplayCoord.FromLocation(settings, location);

        public int NumCols => settings.LocationTypeGridWidths[location.Type];

        // (if no specific row count exists, usually only shows as many rows as needed for the coord, but this looks weird
        // for bases if the pal is in the first row. force at least 3 rows for that case)
        public int NumRows => settings.LocationTypeGridHeights[location.Type] ?? (location.Type == LocationType.Base ? Math.Max(3, ContainerCoord.Row) : ContainerCoord.Row);

        public bool HasTabs => ContainerCoord.Tab != null;

        ILocalizedText tabTitle;
        public ILocalizedText TabTitle => HasTabs ? (tabTitle ??= LocalizationCodes.LC_LOC_PALBOX_TAB.Bind(ContainerCoord.Tab)) : null;

        private List<IContainerLocationPreviewSlotViewModel> slotContents = null;
        public List<IContainerLocationPreviewSlotViewModel> SlotContents =>
            slotContents ??= Enumerable
                .Range(0, NumCols * NumRows)
                .Select(i => PalDisplayCoord.FromLocation(settings, location.Type, i))
                .Select<PalDisplayCoord, IContainerLocationPreviewSlotViewModel>(c =>
                    c.Row == ContainerCoord.Row && c.Column == ContainerCoord.Column
                        ? new FocusedContainerLocationPreviewSlotViewModel()
                        : new EmptyContainerLocationPreviewSlotViewModel()
                )
                .ToList();
    }
}
