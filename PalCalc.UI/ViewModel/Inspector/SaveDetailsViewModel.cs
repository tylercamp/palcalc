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
                    var save = CachedSaveGame.SampleForDesignerView;

                    designerInstance = new SaveDetailsViewModel(
                        save,
                        save.UnderlyingSave.Level.ReadRawCharacterData(),
                        save.UnderlyingSave.Players.Select(p => p.ReadPlayerContent()).SkipNull().ToList()
                    );
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

        public SaveDetailsViewModel(CachedSaveGame csg, RawLevelSaveData rawData, List<PlayerMeta> rawPlayers)
        {
            var playerNamesById = rawData.Characters.Where(c => c.IsPlayer).ToDictionary(c => c.PlayerId.ToString(), c => c.NickName);

            var ownerPlayers = rawPlayers.Select(p => new OwnerViewModel(OwnerType.Player, new HardCodedText(playerNamesById[p.PlayerId]), p.PlayerId)).ToList();
            var ownerGuilds = rawData.Groups.Select(g => new OwnerViewModel(OwnerType.Guild, new HardCodedText(g.Name), g.Id)).ToList();

            var ownersById = (ownerPlayers.Concat(ownerGuilds)).ToDictionary(o => o.Id);

            Containers = rawData.ContainerContents.Select(c =>
            {
                var containedPals = c.Slots
                    .Select(s => csg.OwnedPals.SingleOrDefault(p => p.InstanceId == s.InstanceId.ToString()))
                    .SkipNull()
                    .ToList();

                var containedRawPals = c.Slots
                    .Select(s => rawData.Characters.SingleOrDefault(r => r.InstanceId == s.InstanceId))
                    .SkipNull()
                    .ToList();

                if (rawPlayers.Any(p => p.PartyContainerId == c.Id))
                {
                    var owner = ownersById[rawPlayers.Single(p => p.PartyContainerId == c.Id).PlayerId];
                    return new InspectedContainerDetailsViewModel(owner, containedPals, containedRawPals, c, LocationType.PlayerParty, null);
                }
                else if (rawPlayers.Any(p => p.PalboxContainerId == c.Id))
                {
                    var owner = ownersById[rawPlayers.Single(p => p.PalboxContainerId == c.Id).PlayerId];
                    return new InspectedContainerDetailsViewModel(owner, containedPals, containedRawPals, c, LocationType.Palbox, null);
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

            SelectedContainer = Containers.FirstOrDefault();
            SelectedSlot = SelectedContainer?.Slots?.FirstOrDefault();
        }
    }
}
