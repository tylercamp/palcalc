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
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Inspector
{
    public class PalDetailsProperty
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class PalDetailsViewModel(PalInstance pal, GvasCharacterInstance rawData)
    {
        public Visibility ParsedPropsVisibility => pal == null ? Visibility.Hidden : Visibility.Visible;
        public Visibility RawPropsVisibility => rawData == null ? Visibility.Hidden : Visibility.Visible;

        public List<PalDetailsProperty> PalProperties { get; } = pal == null ? [] :
            new Dictionary<string, object>()
            {
                { "Pal", pal.Pal.Name },
                { "Paldex #", pal.Pal.Id.PalDexNo },
                { "Paldex Is Variant", pal.Pal.Id.IsVariant },
                { "Gender", pal.Gender },
                { "Detected Owner ID", pal.OwnerPlayerId },
            }
            .Select(kvp => new PalDetailsProperty() { Key = kvp.Key, Value = kvp.Value?.ToString() ?? "null" })
            .Concat(pal.Traits.Zip(Enumerable.Range(1, pal.Traits.Count)).Select(t => new PalDetailsProperty() { Key = $"Trait {t.Second}", Value = t.First.Name }))
            .ToList();

        public List<PalDetailsProperty> RawProperties { get; } = rawData == null ? [] :
            new Dictionary<string, object>()
            {
                { "CharacterId", rawData.CharacterId },
                { "Nickname", rawData.NickName },
                { "Level", rawData.Level },
                { "Raw Gender", rawData.Gender },

                { "IsPlayer", rawData.IsPlayer },

                { "Instance ID", rawData.InstanceId },
                { "OwnerPlayerId", rawData.OwnerPlayerId },
                { "OldOwnerPlayerIds", string.Join(", ", rawData.OldOwnerPlayerIds) },

                { "SlotIndex", rawData.SlotIndex },

                { "TalentHp", rawData.TalentHp },
                { "TalentShot", rawData.TalentShot },
                { "TalentMelee", rawData.TalentMelee },
                { "TalentDefense", rawData.TalentDefense },
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

    public class PalContainerSlotViewModel(string instanceId, PalInstance pal, GvasCharacterInstance rawPal) : IContainerSlotViewModel
    {
        public string DisplayName => pal?.Pal?.Name ?? rawPal?.CharacterId ?? InstanceId;
        public ImageSource Icon =>
            pal == null
                ? PalIcon.DefaultIcon
                : PalIcon.Images[pal.Pal];

        public string InstanceId => instanceId;
        public PalInstance Instance => pal;
        public GvasCharacterInstance RawInstance => rawPal;
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
            .Select<PalContainerSlot, IContainerSlotViewModel>(s =>
            {
                if (s == null || s.InstanceId == Guid.Empty) return new EmptyPalContainerSlotViewModel();

                var rawChar = containedRawPals.SingleOrDefault(p => p.InstanceId == s.InstanceId);
                var rawPal = containedPals.SingleOrDefault(p => p.InstanceId == s.InstanceId.ToString());

                return new PalContainerSlotViewModel(s.InstanceId.ToString(), rawPal, rawChar);
            })
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
