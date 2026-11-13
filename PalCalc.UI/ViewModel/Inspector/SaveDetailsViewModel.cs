using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Support.Level;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Inspector.Details;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel.Inspector
{
    public partial class SaveDetailsViewModel : ObservableObject
    {
        // TODO - should eventually show world coordinates too

        private static SaveDetailsViewModel designerInstance = null;
        public static SaveDetailsViewModel DesignerInstance
        {
            get
            {
                if (designerInstance == null)
                {
                    designerInstance = new SaveDetailsViewModel(null, CachedSaveGame.SampleForDesignerView);
                }
                return designerInstance;
            }
        }

        public List<InspectedContainerDetailsViewModel> Containers { get; }

        [ObservableProperty]
        private InspectedContainerDetailsViewModel selectedContainer;

        [NotifyPropertyChangedFor(nameof(SelectedSlotDetails))]
        [ObservableProperty]
        private IContainerSlotDetailsViewModel selectedSlot;

        public PalDetailsViewModel SelectedSlotDetails => SelectedSlot switch
        {
            PalContainerSlotDetailsViewModel pcsvm => new PalDetailsViewModel(pcsvm.Instance, pcsvm.RawInstance),
            _ => null
        };

        private IEnumerable<(PlayerMeta, List<GvasCharacterInstance>)> ReadAllPlayerData(List<PlayersSaveFile> files)
        {
            foreach (var psf in files)
            {
                var playerData = psf.ReadPlayerContent();
                if (playerData == null) continue;

                if (psf.DimensionalPalStorageSaveFile?.IsValid == true)
                    yield return (playerData, psf.DimensionalPalStorageSaveFile.ReadRawCharacters());
                else
                    yield return (playerData, null);
            }
        }

        public SaveDetailsViewModel(ISavesLocation containerLocation, CachedSaveGame csg)
        {
            var rawLevelData = csg.UnderlyingSave.Level.ReadRawCharacterData();
            var rawPlayers = ReadAllPlayerData(csg.UnderlyingSave.Players).ToList();
            var rawGpsPals = containerLocation?.GlobalPalStorage?.IsValid == true ? containerLocation.GlobalPalStorage?.ReadRawCharacters() : null;

            Containers = [
                ..CollectGlobalPalStorageContainers(csg, rawGpsPals),
                ..CollectDimensionalPalStorageContainers(csg, rawLevelData, rawPlayers),
                ..CollectNativeContainers(csg, rawLevelData, rawPlayers.Select(p => p.Item1).ToList()),
            ];

            SelectedContainer = Containers.FirstOrDefault();
            SelectedSlot = SelectedContainer?.Slots?.FirstOrDefault();
        }

        private List<InspectedContainerDetailsViewModel> CollectGlobalPalStorageContainers(CachedSaveGame csg, List<GvasCharacterInstance> rawGpsPals)
        {
            if (rawGpsPals == null) return [];

            var containedPals = csg.OwnedPals.Where(p => p.Location.Type == LocationType.GlobalPalStorage).ToList();

            var rawContainer = new RawPalContainerContents()
            {
                Id = null,
                MaxEntries = rawGpsPals.Count,
                Slots = rawGpsPals.ZipWithIndex().Select(pair =>
                {
                    return new PalContainerSlot()
                    {
                        PalInstanceId = pair.Item1?.PalInstanceId,
                        SlotIndex = pair.Item2
                    };
                }).ToList()
            };

            return [
                new InspectedContainerDetailsViewModel(
                    LocalizationCodes.LC_SAVEINSPECT_GROUP_GLOBAL_PALBOX.Bind().Value,
                    null,
                    containedPals,
                    rawGpsPals,
                    rawContainer,
                    LocationType.GlobalPalStorage,
                    null
                )
            ];
        }

        private List<InspectedContainerDetailsViewModel> CollectDimensionalPalStorageContainers(CachedSaveGame csg, RawLevelSaveData rawLevelData, List<(PlayerMeta, List<GvasCharacterInstance>)> rawDpsData)
        {
            var displayGroupName = LocalizationCodes.LC_SAVEINSPECT_GROUP_DIMENSIONAL_STORAGE.Bind(rawDpsData.Count).Value;

            // (need to `.GroupBy` and `.First` since players can sometimes have duplicate entries in the character list for some reason)
            var playerNamesById = rawLevelData.Characters.Where(c => c.IsPlayer).GroupBy(c => c.PalInstanceId.PlayerUId.ToString()).ToDictionary(g => g.Key, g => g.First().NickName);
            var ownerPlayers = rawDpsData.Select(p => p.Item1).Select(p => new OwnerViewModel(OwnerType.Player, new HardCodedText(playerNamesById[p.PlayerId]), p.PlayerId)).ToList();
            var ownersById = ownerPlayers.ToDictionary(p => p.Id);

            var dpsPals = csg.OwnedPals.Where(p => p.Location.Type == LocationType.DimensionalPalStorage).ToLookup(p => p.Location.ContainerId);

            return rawDpsData.Where(p => p.Item2 != null).Select(pair =>
            {
                var (player, rawPals) = pair;
                var containerId = csg.PalContainers.OfType<DimensionalPalStorageContainer>().FirstOrDefault(c => c.PlayerId == player.PlayerId)?.Id;
                var containedPals = dpsPals[containerId].ToList();

                var rawContainer = new RawPalContainerContents()
                {
                    Id = null,
                    MaxEntries = rawPals.Count,
                    Slots = rawPals.ZipWithIndex().Select(p =>
                    {
                        return new PalContainerSlot()
                        {
                            PalInstanceId = p.Item1.PalInstanceId,
                            SlotIndex = p.Item2,
                        };
                    }).ToList()
                };

                return new InspectedContainerDetailsViewModel(
                    displayGroupName,
                    ownersById[pair.Item1.PlayerId],
                    containedPals,
                    rawPals,
                    rawContainer,
                    LocationType.DimensionalPalStorage,
                    null
                );
            }).ToList();
        }

        private List<InspectedContainerDetailsViewModel> CollectNativeContainers(CachedSaveGame csg, RawLevelSaveData rawData, List<PlayerMeta> rawPlayers)
        {
            var displayGroupName = LocalizationCodes.LC_SAVEINSPECT_GROUP_STANDARD_CONTAINERS.Bind(rawData.ContainerContents.Count).Value;

            var playerNamesById = rawData.Characters.Where(c => c.IsPlayer).GroupBy(c => c.PalInstanceId.PlayerUId.ToString()).ToDictionary(g => g.Key, g => g.First().NickName);

            var ownerPlayers = rawPlayers.Select(p => new OwnerViewModel(OwnerType.Player, new HardCodedText(playerNamesById[p.PlayerId]), p.PlayerId)).ToList();
            var ownerGuilds = rawData.Groups.Select(g => new OwnerViewModel(OwnerType.Guild, new HardCodedText(g.Name), g.Id)).ToList();

            var ownersById = (ownerPlayers.Concat(ownerGuilds)).ToDictionary(o => o.Id);
            var palsByContainer = csg.OwnedPals.Where(p => p.ExternalStorageContainerId == null).ToLookup(p => p.Location.ContainerId);
            var rawCharactersById = rawData.Characters.ToLookup(p => p.PalInstanceId);

            return rawData.ContainerContents.Select(c =>
            {
                var slotIds = c.Slots
                    .Where(s => s.PalInstanceId.HasValue)
                    .Select(s => s.PalInstanceId.Value)
                    .ToHashSet();

                // The inspector rereads the slots, so a cached Pal may have left this container.
                var containedPals = palsByContainer[c.Id]
                    .Where(p => p.PalInstanceId is { } id && slotIds.Contains(id))
                    .ToList();
                var containedRawPals = slotIds.SelectMany(id => rawCharactersById[id]).ToList();

                if (rawPlayers.Any(p => p.PartyContainerId == c.Id))
                {
                    var owner = ownersById[rawPlayers.Single(p => p.PartyContainerId == c.Id).PlayerId];
                    return new InspectedContainerDetailsViewModel(displayGroupName, owner, containedPals, containedRawPals, c, LocationType.PlayerParty, null);
                }
                else if (rawPlayers.Any(p => p.PalboxContainerId == c.Id))
                {
                    var owner = ownersById[rawPlayers.Single(p => p.PalboxContainerId == c.Id).PlayerId];
                    return new InspectedContainerDetailsViewModel(displayGroupName, owner, containedPals, containedRawPals, c, LocationType.Palbox, null);
                }
                else
                {
                    var matchingBase = rawData.Bases.FirstOrDefault(b => b.ContainerId.ToString() == c.Id);
                    var matchingCage = rawData.MapObjects.FirstOrDefault(m => m.ObjectId == GvasMapObject.ViewingCageObjectId && m.PalContainerId?.ToString() == c.Id);

                    VectorLiteral? position = null;
                    LocationType? definiteType = null;
                    string ownerId = null;
                    if (matchingBase != null && matchingBase.OwnerGroupId != Guid.Empty)
                    {
                        ownerId = matchingBase.OwnerGroupId.ToString();
                        definiteType = LocationType.Base;
                        position = matchingBase.Position;
                    }
                    else if (matchingCage != null)
                    {
                        var cageBase = rawData.Bases.FirstOrDefault(b => matchingCage.OwnerBaseId.ToString() == b.Id);
                        if (cageBase != null && cageBase.OwnerGroupId != Guid.Empty)
                            ownerId = cageBase.OwnerGroupId.ToString();

                        position = cageBase?.Position;

                        definiteType = LocationType.ViewingCage;
                    }


                    if (ownerId == null && containedPals.Count > 0)
                    {
                        var mostCommonType = containedPals.GroupBy(p => p.Location).MaxBy(g => g.Count()).Key;
                        var owners = containedPals.Select(p => p.OwnerPlayerId).Distinct().ToList();

                        if (owners.Count == 1)
                        {
                            ownerId = owners.First();
                        }
                        else
                        {
                            ownerId = owners
                                .SelectMany(o => rawData.Groups.Where(g => g.MemberIds.Contains(o)).Select(g => g.Id))
                                .MostCommonOrDefault();
                        }
                    }

                    var owner = ownerId == null
                        ? OwnerViewModel.Unknown
                        : ownersById.GetValueOrElse(ownerId, OwnerViewModel.UnknownWithId(ownerId));

                    return new InspectedContainerDetailsViewModel(
                        displayGroupName,
                        owner,
                        containedPals,
                        containedRawPals,
                        c,
                        definiteType ?? containedPals.FirstOrDefault()?.Location?.Type,
                        position == null ? null : new WorldCoord()
                        {
                            X = (matchingBase?.Position ?? position).Value.x,
                            Y = (matchingBase?.Position ?? position).Value.y,
                            Z = (matchingBase?.Position ?? position).Value.z
                        }
                    );
                }
            }).ToList();
        }
    }
}
