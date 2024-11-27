using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.Mapped
{
    public interface IPalRefLocationViewModel
    {
        // locations need a cached save game to fetch owner info, but if the cache was unavailable when
        // the app was started, these will be left empty.
        bool NeedsRefresh { get; }
    }

    public class MapCoordViewModel(WorldCoord worldCoord, int iconSizePercent = 10)
    {
        public WorldCoord WorldCoords => worldCoord;

        public MapCoord DisplayCoords { get; } = MapCoord.UIFromWorldCoord(worldCoord);
        public MapCoord NormalizedCoords { get; } = MapCoord.NormalizedFromWorldCoord(worldCoord);

        public string DisplayCoordsText => $"{DisplayCoords.X:N0}, {DisplayCoords.Y:N0}";
        public string WorldCoordsText => $"{WorldCoords.X:N0}, {WorldCoords.Y:N0}, {WorldCoords.Z:N0}";

        // (don't like the use of `Grid` for this, but this is the most straightforward way (afaik) to get
        // reliable + scaling positioning of the coord within the MapView)

        // note: the normalized coords are used directly for positioning here, but the map locs at
        // (-1000,-1000) and (1000,1000) in-game don't exactly match the bottom-left + top-right locs
        // in the image displayed in Pal Calc. i.e. the resulting coords aren't 100% correct

        private List<RowDefinition> gridRows;
        public List<RowDefinition> GridRows => gridRows ??=
        [
            // (Rows used for Y positioning, Y=0 is top and Y=Max is bottom in WPF, but the Palworld map has this inverted)
            new RowDefinition() { Height = new GridLength((100 - iconSizePercent) * (1 - NormalizedCoords.Y), GridUnitType.Star) },
            new RowDefinition() { Height = new GridLength(iconSizePercent, GridUnitType.Star) },
            new RowDefinition() { Height = new GridLength((100 - iconSizePercent) * NormalizedCoords.Y, GridUnitType.Star) }
        ];

        private List<ColumnDefinition> gridColumns;
        public List<ColumnDefinition> GridColumns => gridColumns ??=
        [
            // (Columns used for X positioning, normalized coords used directly)
            new ColumnDefinition() { Width = new GridLength((100 - iconSizePercent) * NormalizedCoords.X, GridUnitType.Star) },
            new ColumnDefinition() { Width = new GridLength(iconSizePercent, GridUnitType.Star) },
            new ColumnDefinition() { Width = new GridLength((100 - iconSizePercent) * (1 - NormalizedCoords.X), GridUnitType.Star) }
        ];

        public static MapCoordViewModel DesignerInstance { get; } = new MapCoordViewModel(new WorldCoord() { X = 10000, Y = 10000, Z = 0 });

        public static MapCoordViewModel FromCoord(WorldCoord coord, int iconSizePercent = 10) => coord == null ? null : new MapCoordViewModel(coord);
        public static MapCoordViewModel FromBase(BaseInstance inst, int iconSizePercent = 10) => FromCoord(inst?.Position, iconSizePercent);
    }

    public class CompositePalRefLocationViewModel : IPalRefLocationViewModel
    {
        public CompositePalRefLocationViewModel(CachedSaveGame source, CompositeRefLocation location)
        {
            ModelObject = location;

            MaleViewModel = new SpecificPalRefLocationViewModel(source, location.MaleLoc);
            FemaleViewModel = new SpecificPalRefLocationViewModel(source, location.FemaleLoc);
        }

        public CompositeRefLocation ModelObject { get; }

        public SpecificPalRefLocationViewModel MaleViewModel { get; }
        public SpecificPalRefLocationViewModel FemaleViewModel { get; }

        public bool NeedsRefresh => MaleViewModel.NeedsRefresh || FemaleViewModel.NeedsRefresh;
    }

    public class SpecificPalRefLocationViewModel : IPalRefLocationViewModel
    {
        private static SpecificPalRefLocationViewModel designerInstance;
        public static SpecificPalRefLocationViewModel DesignerInstance => designerInstance ??= new SpecificPalRefLocationViewModel();
        private SpecificPalRefLocationViewModel()
        {
            IsSinglePlayer = true;
        }

        public SpecificPalRefLocationViewModel(CachedSaveGame source, IPalRefLocation location)
        {
            if (location is CompositeRefLocation) throw new InvalidOperationException();

            ModelObject = location;

            var ownedLoc = location as OwnedRefLocation;
            if (ownedLoc == null) return;

            NeedsRefresh = source == null;

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                // for XAML designer preview
                IsSinglePlayer = true;
                LocationOwner = new HardCodedText(ownedLoc.OwnerId);
            }
            else
            {
                IsSinglePlayer = source == null || source.Players.Count == 1;

                if (ownedLoc.Location.Type != LocationType.Custom)
                {
                    var rawOwnerName = source?.PlayersById?.GetValueOrDefault(ownedLoc.OwnerId)?.Name;

                    ILocalizedText ownerName = rawOwnerName != null
                        ? new HardCodedText(rawOwnerName)
                        : LocalizationCodes.LC_UNKNOWN_PLAYER.Bind();


                    // (old cached saves may be missing PalContainers, which would cause GuildsByContainerId to be null)
                    var guildFromDirect = ownedLoc.Location.ContainerId == null ? null : source?.GuildsByContainerId?.GetValueOrDefault(ownedLoc.Location.ContainerId);
                    var guildFromPlayer = source?.GuildsByPlayerId?.GetValueOrDefault(ownedLoc.OwnerId);

                    var ownerGuild = guildFromDirect ?? guildFromPlayer;

                    var isGuildOwner = (ownedLoc.Location.Type == LocationType.Base || ownedLoc.Location.Type == LocationType.ViewingCage) && ownerGuild?.MemberIds?.Count > 1;

                    if (isGuildOwner)
                    {
                        if (ownerGuild?.Name != null) LocationOwner = new HardCodedText(ownerGuild.Name);
                        else LocationOwner = LocalizationCodes.LC_UNKNOWN_GUILD.Bind();
                    }
                    else
                    {
                        LocationOwner = ownerName;
                    }
                }
                else
                {
                    LocationOwner = null;
                }
            }

            BaseInstance sourceBase = null;

            switch (ownedLoc.Location.Type)
            {
                case LocationType.PlayerParty:
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_PARTY.Bind(ownedLoc.Location.Index + 1);
                    break;

                case LocationType.Custom:
                    LocationCoordDescription = LocalizationCodes.LC_CUSTOM_CONTAINER.Bind(ownedLoc.Location.ContainerId);
                    break;

                case LocationType.Base:
                    sourceBase = source.Bases?.FirstOrDefault(b => b.Container?.Id == ownedLoc.Location.ContainerId);

                    var baseCoord = PalDisplayCoord.FromLocation(ownedLoc.Location);
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_BASE.Bind(
                        new
                        {
                            X = baseCoord.X,
                            Y = baseCoord.Y,
                        }
                    );
                    break;

                case LocationType.Palbox:
                    var pboxCoord = PalDisplayCoord.FromLocation(ownedLoc.Location);
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_PALBOX.Bind(
                        new
                        {
                            Tab = pboxCoord.Tab.Value,
                            X = pboxCoord.X,
                            Y = pboxCoord.Y,
                        }
                    );
                    break;

                case LocationType.ViewingCage:
                    sourceBase = source.Bases?.FirstOrDefault(b => b.ViewingCages.Any(c => c.Id == ownedLoc.Location.ContainerId));

                    var cageCoord = PalDisplayCoord.FromLocation(ownedLoc.Location);
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_VIEWING_CAGE.Bind(
                        new
                        {
                            X = cageCoord.X,
                            Y = cageCoord.Y,
                        }
                    );
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (LocationOwner != null)
                LocationOwnerDescription = LocalizationCodes.LC_LOC_OWNED_BY.Bind(LocationOwner);

            var mapCoord = MapCoordViewModel.FromBase(sourceBase);
            if (mapCoord != null) MapLocationPreview = new MapLocationPreviewViewModel(mapCoord);

            if (ownedLoc.Location.Type != LocationType.Custom)
                ContainerLocationPreview = new ContainerLocationPreviewViewModel(ownedLoc.Location);
        }

        public bool IsSinglePlayer { get; }
        public bool NeedsRefresh { get; }

        public IPalRefLocation ModelObject { get; }
        public Visibility Visibility => ModelObject is OwnedRefLocation ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OwnerVisibility => IsSinglePlayer || LocationOwner == null ? Visibility.Collapsed : Visibility.Visible;

        public ILocalizedText LocationOwner { get; }
        
        public ILocalizedText LocationOwnerDescription { get; }

        public ILocalizedText LocationCoordDescription { get; }

        public bool HasPreview => ContainerLocationPreview != null || MapLocationPreview != null;

        public ContainerLocationPreviewViewModel ContainerLocationPreview { get; }
        public MapLocationPreviewViewModel MapLocationPreview { get; }
    }

    public class WildPalRefLocationViewModel : IPalRefLocationViewModel
    {
        public bool NeedsRefresh => false;
    }
}
