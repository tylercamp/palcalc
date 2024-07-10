using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Support.Level;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace PalCalc.UI.ViewModel
{
    public enum OwnerType { Player, Guild, Unknown }

    public class OwnerViewModel(OwnerType type, string name, string id)
    {
        public OwnerType Type { get; } = type;
        public string Name { get; } = name;
        public string Id { get; } = id;

        public static OwnerViewModel Unknown => new OwnerViewModel(OwnerType.Unknown, "Unknown", "null");
        public static OwnerViewModel UnknownWithId(string id) => new OwnerViewModel(OwnerType.Unknown, "Unknown", id);
    }

    public interface IContainerSlotViewModel { }

    public class PalContainerSlotViewModel(PalInstance pal) : IContainerSlotViewModel
    {
        public PalViewModel Pal { get; } = new PalViewModel(pal.Pal);
        public PalGender Gender => pal.Gender;
        public TraitCollectionViewModel Traits { get; } = new TraitCollectionViewModel(pal.Traits.Select(t => new TraitViewModel(t)));
    }

    public class EmptyPalContainerSlotViewModel : IContainerSlotViewModel { }

    public partial class InspectedContainerViewModel(
        OwnerViewModel owner,
        List<PalInstance> containedPals,
        PalContainer rawContainer,
        LocationType? locationType
    ) : ObservableObject
    {
        // TODO - change from SingleOrDefault to Single when human pals are handled
        public List<IContainerSlotViewModel> Slots { get; } = rawContainer.Slots
            .Select(s => containedPals.SingleOrDefault(p => p.InstanceId == s.InstanceId.ToString()))
            .Select<PalInstance, IContainerSlotViewModel>(p => p == null ? new EmptyPalContainerSlotViewModel() : new PalContainerSlotViewModel(p))
            .ToList();

        public int TotalSlots => rawContainer.MaxEntries;
        public int UsedSlots => rawContainer.NumEntries;

        public string Id => rawContainer.Id;
        public OwnerViewModel Owner { get; } = owner;
        public string Type => locationType?.Label() ?? "Unknown";
    }

    public partial class SaveInspectorViewModel : ObservableObject
    {
        private static SaveInspectorViewModel designerInstance = null;
        public static SaveInspectorViewModel DesignerInstance =>
            designerInstance ??= new SaveInspectorViewModel(
                DirectSavesLocation.AllLocal
                    .SelectMany(l => l.ValidSaveGames)
                    .OrderBy(g => g.LastModified)
                    .Select(g => CachedSaveGame.FromSaveGame(g, PalDB.LoadEmbedded()))
                    .First()
            );

        public List<InspectedContainerViewModel> Containers { get; }

        public SaveInspectorViewModel(CachedSaveGame csg)
        {
            var rawData = csg.UnderlyingSave.Level.ReadRawCharacterData();
            var players = csg.UnderlyingSave.Players.Select(p => p.ReadPlayerContent()).ToList();

            var playerNamesById = rawData.Characters.Where(c => c.IsPlayer).ToDictionary(c => c.PlayerId.ToString(), c => c.NickName);

            var ownerPlayers = players.Select(p => new OwnerViewModel(OwnerType.Player, playerNamesById[p.PlayerId], p.PlayerId)).ToList();
            var ownerGuilds = rawData.Groups.Select(g => new OwnerViewModel(OwnerType.Guild, g.Name, g.Id)).ToList();

            var ownersById = (ownerPlayers.Concat(ownerGuilds)).ToDictionary(o => o.Id);

            Containers = rawData.Containers.Select(c =>
            {
                // TODO - handle characters which aren't properly detected as pals (e.g. captured humans)
                var containedPals = c.Slots
                    .Select(s => csg.OwnedPals.SingleOrDefault(p => p.InstanceId == s.InstanceId.ToString()))
                    .SkipNull()
                    .ToList();

                if (players.Any(p => p.PartyContainerId == c.Id))
                {
                    var owner = ownersById[players.Single(p => p.PartyContainerId == c.Id).PlayerId];
                    return new InspectedContainerViewModel(owner, containedPals, c, LocationType.PlayerParty);
                }
                else if (players.Any(p => p.PalboxContainerId == c.Id))
                {
                    var owner = ownersById[players.Single(p => p.PalboxContainerId == c.Id).PlayerId];
                    return new InspectedContainerViewModel(owner, containedPals, c, LocationType.Palbox);
                }
                else
                {
                    if (containedPals.Count > 0)
                    {
                        var mostCommonType = containedPals.GroupBy(p => p.Location).MaxBy(g => g.Count()).Key;
                        var owners = containedPals.Select(p => p.OwnerPlayerId).Distinct().ToList();
                        if (owners.Count == 1)
                        {
                            var ownerId = owners.First();
                            return new InspectedContainerViewModel(ownersById.GetValueOrElse(ownerId, OwnerViewModel.UnknownWithId(ownerId)), containedPals, c, containedPals.First().Location.Type);
                        }
                        else
                        {
                            var mostCommonGuild = owners
                                .Select(o => rawData.Groups.Single(g => g.MemberIds.Contains(o)).Id)
                                .GroupBy(id => id)
                                .MaxBy(g => g.Count())
                                .Key;

                            return new InspectedContainerViewModel(ownersById.GetValueOrElse(mostCommonGuild, OwnerViewModel.UnknownWithId(mostCommonGuild)), containedPals, c, containedPals.First().Location.Type);
                        }
                    }
                    else
                    {
                        return new InspectedContainerViewModel(OwnerViewModel.Unknown, containedPals, c, null);
                    }
                }
            }).ToList();
        }
    }
}
