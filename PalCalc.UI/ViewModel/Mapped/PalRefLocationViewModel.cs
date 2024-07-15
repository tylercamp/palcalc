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

                var rawOwnerName = source?.PlayersById?.GetValueOrDefault(ownedLoc.OwnerId)?.Name;
                ILocalizedText ownerName = rawOwnerName != null
                    ? new HardCodedText(rawOwnerName)
                    : Translator.Translations[LocalizationCodes.LC_UNKNOWN_PLAYER].Bind();

                var ownerGuild = source?.GuildsByPlayerId?.GetValueOrDefault(ownedLoc.OwnerId);

                var isGuildOwner = ownedLoc.Location.Type == LocationType.Base && ownerGuild?.MemberIds?.Count > 1;

                if (isGuildOwner)
                {
                    if (ownerGuild?.Name != null) LocationOwner = new HardCodedText(ownerGuild.Name);
                    else LocationOwner = Translator.Translations[LocalizationCodes.LC_UNKNOWN_GUILD].Bind();
                }
                else
                {
                    LocationOwner = ownerName;
                }
            }

            switch (ownedLoc.Location.Type)
            {
                case LocationType.PlayerParty:
                    LocationCoordDescription = Translator.Translations[LocalizationCodes.LC_LOC_COORD_PARTY].Bind(
                        new() { { "SlotNum", ownedLoc.Location.Index + 1 } }
                    );
                    break;

                case LocationType.Base:
                    var baseCoord = BaseCoord.FromSlotIndex(ownedLoc.Location.Index);
                    LocationCoordDescription = Translator.Translations[LocalizationCodes.LC_LOC_COORD_BASE].Bind(
                        new()
                        {
                            { "X", baseCoord.X },
                            { "Y", baseCoord.Y },
                        }
                    );
                    break;

                case LocationType.Palbox:
                    var pboxCoord = PalboxCoord.FromSlotIndex(ownedLoc.Location.Index);
                    LocationCoordDescription = Translator.Translations[LocalizationCodes.LC_LOC_COORD_PALBOX].Bind(
                        new()
                        {
                            { "Tab", pboxCoord.Tab },
                            { "X", pboxCoord.X },
                            { "Y", pboxCoord.Y },
                        }
                    );
                    break;
            }

            LocationOwnerDescription = Translator.Translations[LocalizationCodes.LC_LOC_OWNED_BY].Bind(
                new()
                {
                    { "Owner", LocationOwner }
                }
            );
        }

        public bool IsSinglePlayer { get; }
        public bool NeedsRefresh { get; }

        public IPalRefLocation ModelObject { get; }
        public Visibility Visibility => ModelObject is OwnedRefLocation ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OwnerVisibility => IsSinglePlayer ? Visibility.Collapsed : Visibility.Visible;

        public ILocalizedText LocationOwner { get; }
        
        public ILocalizedText LocationOwnerDescription { get; }

        public ILocalizedText LocationCoordDescription { get; }
    }

    public class WildPalRefLocationViewModel : IPalRefLocationViewModel
    {
        public bool NeedsRefresh => false;
    }
}
