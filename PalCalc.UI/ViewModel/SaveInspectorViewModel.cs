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
    public class PalDetailsProperty
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class PalDetailsViewModel(PalInstance pal, GvasCharacterInstance rawData)
    {
        public List<PalDetailsProperty> PalProperties { get; } =
            new Dictionary<string, object>()
            {
                { "Instance ID", pal.InstanceId },
                { "Nickname", pal.NickName },
                { "Level", pal.Level },
                { "Owner Player ID", pal.OwnerPlayerId },
                { "Pal", pal.Pal.Name },
                { "Internal Name", pal.Pal.InternalName },
                { "Gender", pal.Gender },
                { "IV - HP", pal.IV_HP },
                { "IV - Shot", pal.IV_Shot },
                { "IV - Melee", pal.IV_Melee },
                { "IV - Defense", pal.IV_Defense },
            }
            .Select(kvp => new PalDetailsProperty() { Key = kvp.Key, Value = kvp.Value?.ToString() ?? "null" })
            .Concat(pal.Traits.Zip(Enumerable.Range(1, pal.Traits.Count)).Select(t => new PalDetailsProperty() { Key = $"Trait {t.Second}", Value = t.First.Name }))
            .ToList();

        public List<PalDetailsProperty> RawProperties { get; } =
            new Dictionary<string, object>()
            {
                { "Is Player", rawData.IsPlayer },
                { "Owner Player ID", rawData.OwnerPlayerId },
                { "Old Owner Player IDs", string.Join(", ", rawData.OldOwnerPlayerIds) },
                { "Slot Index", rawData.SlotIndex },
                { "Character ID", rawData.CharacterId },
                { "Gender", rawData.Gender },
            }
            .Select(kvp => new PalDetailsProperty() { Key = kvp.Key, Value = kvp.Value?.ToString() ?? "null" })
            .Concat(rawData.Traits.Zip(Enumerable.Range(1, rawData.Traits.Count)).Select(t => new PalDetailsProperty() { Key = $"Trait {t.Second}", Value = t.First }))
            .ToList();
    }

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

    public class PalContainerSlotViewModel(PalInstance pal, GvasCharacterInstance rawPal) : IContainerSlotViewModel
    {
        public PalInstance Instance => pal;
        public GvasCharacterInstance RawInstance => rawPal;
        public PalViewModel Pal { get; } = new PalViewModel(pal.Pal);
        public PalGender Gender => pal.Gender;
        public TraitCollectionViewModel Traits { get; } = new TraitCollectionViewModel(pal.Traits.Select(t => new TraitViewModel(t)));
    }

    public class EmptyPalContainerSlotViewModel : IContainerSlotViewModel { }

    public partial class InspectedContainerViewModel(
        OwnerViewModel owner,
        List<PalInstance> containedPals,
        List<GvasCharacterInstance> containedRawPals,
        PalContainer rawContainer,
        LocationType? locationType
    ) : ObservableObject
    {
        // TODO - change from SingleOrDefault to Single when human pals are handled
        public List<IContainerSlotViewModel> Slots { get; } = Enumerable.Range(0, rawContainer.MaxEntries)
            .Select(i => rawContainer.Slots.SingleOrDefault(s => s.SlotIndex == i))
            .Select(s => s == null ? null : containedPals.SingleOrDefault(p => p.InstanceId == s.InstanceId.ToString()))
            .Select<PalInstance, IContainerSlotViewModel>(p =>
                p == null
                    ? new EmptyPalContainerSlotViewModel()
                    : new PalContainerSlotViewModel(p, containedRawPals.Single(r => r.InstanceId.ToString() == p.InstanceId))
            )
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
                    .OrderByDescending(g => g.LastModified)
                    .Select(g => CachedSaveGame.FromSaveGame(g, PalDB.LoadEmbedded()))
                    .First()
            );

        public List<InspectedContainerViewModel> Containers { get; }

        [ObservableProperty]
        private InspectedContainerViewModel selectedContainer;

        [NotifyPropertyChangedFor(nameof(SelectedSlotDetails))]
        [ObservableProperty]
        private IContainerSlotViewModel selectedSlot;

        public PalDetailsViewModel SelectedSlotDetails => SelectedSlot switch
        {
            PalContainerSlotViewModel pcsvm => new PalDetailsViewModel(pcsvm.Instance, pcsvm.RawInstance),
            _ => null
        };

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

                var containedRawPals = c.Slots
                    .Select(s => rawData.Characters.SingleOrDefault(r => r.InstanceId == s.InstanceId))
                    .SkipNull()
                    .ToList();

                if (players.Any(p => p.PartyContainerId == c.Id))
                {
                    var owner = ownersById[players.Single(p => p.PartyContainerId == c.Id).PlayerId];
                    return new InspectedContainerViewModel(owner, containedPals, containedRawPals, c, LocationType.PlayerParty);
                }
                else if (players.Any(p => p.PalboxContainerId == c.Id))
                {
                    var owner = ownersById[players.Single(p => p.PalboxContainerId == c.Id).PlayerId];
                    return new InspectedContainerViewModel(owner, containedPals, containedRawPals, c, LocationType.Palbox);
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
                            return new InspectedContainerViewModel(
                                ownersById.GetValueOrElse(ownerId, OwnerViewModel.UnknownWithId(ownerId)),
                                containedPals,
                                containedRawPals,
                                c,
                                containedPals.First().Location.Type
                            );
                        }
                        else
                        {
                            var mostCommonGuild = owners
                                .Select(o => rawData.Groups.Single(g => g.MemberIds.Contains(o)).Id)
                                .GroupBy(id => id)
                                .MaxBy(g => g.Count())
                                .Key;

                            return new InspectedContainerViewModel(
                                ownersById.GetValueOrElse(mostCommonGuild, OwnerViewModel.UnknownWithId(mostCommonGuild)),
                                containedPals,
                                containedRawPals,
                                c,
                                containedPals.First().Location.Type
                            );
                        }
                    }
                    else
                    {
                        return new InspectedContainerViewModel(OwnerViewModel.Unknown, containedPals, containedRawPals, c, null);
                    }
                }
            }).ToList();

            SelectedContainer = Containers.FirstOrDefault();
            SelectedSlot = SelectedContainer?.Slots?.FirstOrDefault();
        }
    }
}
