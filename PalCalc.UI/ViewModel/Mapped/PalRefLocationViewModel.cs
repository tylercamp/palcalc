using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.PalDerived;
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

    public class CompositePalRefLocationViewModel : IPalRefLocationViewModel
    {
        public CompositePalRefLocationViewModel(CachedSaveGame source, GameSettings settings, CompositeRefLocation location)
        {
            ModelObject = location;

            MaleViewModel = new SpecificPalRefLocationViewModel(source, settings, location.MaleLoc);
            FemaleViewModel = new SpecificPalRefLocationViewModel(source, settings, location.FemaleLoc);
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

        public SpecificPalRefLocationViewModel(CachedSaveGame source, GameSettings settings, IPalRefLocation location)
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

                if (ownedLoc.Location.Type != LocationType.Custom && ownedLoc.Location.Type != LocationType.GlobalPalStorage)
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
                    sourceBase = source?.Bases?.FirstOrDefault(b => b.Container?.Id == ownedLoc.Location.ContainerId);

                    var baseCoord = PalDisplayCoord.FromLocation(settings, ownedLoc.Location);
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_BASE.Bind(
                        new
                        {
                            X = baseCoord.X,
                            Y = baseCoord.Y,
                        }
                    );
                    break;

                case LocationType.Palbox:
                    var pboxCoord = PalDisplayCoord.FromLocation(settings, ownedLoc.Location);
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
                    sourceBase = source?.Bases?.FirstOrDefault(b => b.ViewingCages.Any(c => c.Id == ownedLoc.Location.ContainerId));

                    var cageCoord = PalDisplayCoord.FromLocation(settings, ownedLoc.Location);
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_VIEWING_CAGE.Bind(
                        new
                        {
                            X = cageCoord.X,
                            Y = cageCoord.Y,
                        }
                    );
                    break;

                case LocationType.DimensionalPalStorage:
                    var dpsCoord = PalDisplayCoord.FromLocation(settings, ownedLoc.Location);
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_DPS.Bind(
                        new
                        {
                            Tab = dpsCoord.Tab,
                            X = dpsCoord.X,
                            Y = dpsCoord.Y
                        }
                    );
                    break;

                case LocationType.GlobalPalStorage:
                    var gpsCoord = PalDisplayCoord.FromLocation(settings, ownedLoc.Location);
                    LocationCoordDescription = LocalizationCodes.LC_LOC_COORD_GPS.Bind(
                        new
                        {
                            Tab = gpsCoord.Tab,
                            X = gpsCoord.X,
                            Y = gpsCoord.Y
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
                ContainerLocationPreview = new ContainerLocationPreviewViewModel(settings, ownedLoc.Location);
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
