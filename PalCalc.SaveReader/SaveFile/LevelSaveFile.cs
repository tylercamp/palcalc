﻿using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Support.Level;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PalCalc.SaveReader.SaveFile
{
    public class LevelSaveData
    {
        public List<PalInstance> Pals { get; set; }
        public List<PlayerInstance> Players { get; set; }
        public List<GuildInstance> Guilds { get; set; }
        public List<BaseInstance> Bases { get; set; }
        public List<IPalContainer> PalContainers { get; set; }
    }

    public class RawLevelSaveData
    {
        public List<GvasCharacterInstance> Characters { get; set; }
        public List<RawPalContainerContents> ContainerContents { get; set; }
        public List<GuildInstance> Groups { get; set; }
        public List<GvasBaseInstance> Bases { get; set; }
        public List<GvasMapObject> MapObjects { get; set; }
    }

    public class LevelSaveFile(IFileSource files) : ISaveFile(files)
    {
        private static ILogger logger = Log.ForContext<LevelSaveFile>();

        private Guid MostCommonOwner(RawPalContainerContents container, Dictionary<Guid, Guid> palOwnersByInstanceId) => container.Slots.GroupBy(s => palOwnersByInstanceId.GetValueOrElse(s.InstanceId, Guid.Empty)).MaxBy(g => g.Count())?.Key ?? Guid.Empty;

        public virtual RawLevelSaveData ReadRawCharacterData()
        {
            var containerVisitor = new PalContainerVisitor();
            var instanceVisitor = new WorldSaveData_CharacterInstanceVisitor();
            var groupVisitor = new GroupVisitor();
            var baseVisitor = new BaseVisitor();
            var mapObjectVisitor = new MapObjectVisitor(
                GvasMapObject.PalBoxObjectId,
                GvasMapObject.ViewingCageObjectId,
                GvasMapObject.GlobalPalBoxObjectId,
                GvasMapObject.DimensionalPalStorageObjectId
            );
            VisitGvas(containerVisitor, instanceVisitor, groupVisitor, baseVisitor, mapObjectVisitor);

            return new RawLevelSaveData()
            {
                Characters = instanceVisitor.Result,
                ContainerContents = containerVisitor.CollectedContainers,
                Groups = groupVisitor.Result,
                Bases = baseVisitor.Result,
                MapObjects = mapObjectVisitor.Result,
            };
        }

        private List<IPalContainer> CollectDefiniteContainers(RawLevelSaveData parsed, List<PlayerMeta> players, Dictionary<Guid, Guid> instanceOwners)
        {
            return parsed.ContainerContents.Select<RawPalContainerContents, IPalContainer>(c =>
            {
                var fromBase = parsed.Bases.FirstOrDefault(b => b.ContainerId.ToString() == c.Id);
                var fromCage = parsed.MapObjects.FirstOrDefault(m => m.ObjectId == GvasMapObject.ViewingCageObjectId && m.PalContainerId != null && m.PalContainerId.ToString() == c.Id);
                var fromParty = players.FirstOrDefault(p => p.PartyContainerId == c.Id);
                var fromPalbox = players.FirstOrDefault(p => p.PalboxContainerId == c.Id);

                if (fromBase != null)
                {
                    return new BasePalContainer()
                    {
                        Id = c.Id,
                        BaseId = fromBase.Id,
                    };
                }
                else if (fromCage != null)
                {
                    return new ViewingCageContainer()
                    {
                        Id = c.Id,
                        BaseId = fromCage.OwnerBaseId.ToString()
                    };
                }
                else if (fromParty != null)
                {
                    return new PlayerPartyContainer()
                    {
                        Id = c.Id,
                        PlayerId = fromParty.PlayerId
                    };
                }
                else if (fromPalbox != null)
                {
                    return new PalboxPalContainer()
                    {
                        Id = c.Id,
                        PlayerId = fromPalbox.PlayerId
                    };
                }
                else
                {
                    return null;
                }
            }).SkipNull().ToList();
        }

        private List<IPalContainer> CollectContainers(
            GameSettings settings,
            RawLevelSaveData parsed,
            List<PlayerMeta> players,
            IEnumerable<DimensionalPalStorageContainer> dpsContainers,
            GlobalPalStorageContainer gpsContainer
        )
        {
            var instanceOwners = parsed.Characters.Where(c => c.OwnerPlayerId != null).ToDictionary(c => c.InstanceId, c => c.OwnerPlayerId.Value);
            var definiteContainers = CollectDefiniteContainers(parsed, players, instanceOwners).Concat(dpsContainers).ToList();

            if (gpsContainer != null) definiteContainers.Add(gpsContainer);

            int numImpliedBases = 0;

            foreach (var unexpected in parsed.ContainerContents.Where(c => !definiteContainers.Any(d => d.Id == c.Id)).OrderByDescending(c => c.MaxEntries).ToList())
            {
                var allOwners = unexpected.Slots
                    .Where(s => instanceOwners.ContainsKey(s.InstanceId))
                    .GroupBy(s => instanceOwners[s.InstanceId])
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .Where(id => id != Guid.Empty)
                    .ToList();

                var possiblePartyOwners = allOwners.Where(id => !definiteContainers.OfType<PlayerPartyContainer>().Any(c => c.PlayerId == id.ToString())).ToList();
                var possiblePalBoxOwners = allOwners.Where(id => !definiteContainers.OfType<PalboxPalContainer>().Any(c => c.PlayerId == id.ToString())).ToList();

                var allowedTypes = new List<LocationType>();
                if (unexpected.MaxEntries <= settings.PlayerPartySize)
                {
                    definiteContainers.Add(new PlayerPartyContainer()
                    {
                        Id = unexpected.Id,
                        PlayerId = possiblePartyOwners.FirstOrDefault().ToString()
                    });
                }
                else if (possiblePalBoxOwners.Count > 0 && unexpected.MaxEntries > settings.PlayerPartySize)
                {
                    definiteContainers.Add(new PalboxPalContainer()
                    {
                        Id = unexpected.Id,
                        PlayerId = possiblePalBoxOwners.First().ToString()
                    });
                }
                else
                {
                    // TODO - how does the owner guild fit into this?
                    definiteContainers.Add(new BasePalContainer()
                    {
                        Id = unexpected.Id,
                        BaseId = $"UNKNOWN_{++numImpliedBases}"
                    });
                }
            }

            return definiteContainers;
        }

        // note: `settings` only used for pal container sizes, in case player saves are unavailable and we need to
        //       infer the container type based on their size
        public virtual LevelSaveData ReadCharacterData(PalDB db, GameSettings settings, List<PlayersSaveFile> playersFiles, GlobalPalStorageSaveFile gpsFile)
        {
            var players = new List<PlayerMeta>();
            var dpsData = new List<DimensionalPalStorageData>();
            var dpsContainerByPlayerId = new Dictionary<string, DimensionalPalStorageContainer>();

            foreach (var pf in playersFiles)
            {
                var playerData = pf.ReadPlayerContent();
                if (playerData == null) continue;

                var dpsContainerId = $"{playerData.PlayerId}_DPS";
                var dps = pf.DimensionalPalStorageSaveFile?.ReadPals(dpsContainerId);

                players.Add(playerData);
                if (dps != null)
                {
                    dpsData.Add(dps);
                    dpsContainerByPlayerId.Add(playerData.PlayerId, new DimensionalPalStorageContainer()
                    {
                        PlayerId = playerData.PlayerId,
                        Id = dpsContainerId,
                    });
                }
            }

            var parsed = ReadRawCharacterData();

            var gpsData = gpsFile?.ReadPals("GLOBAL_PAL_STORAGE");
            var gpsContainer = gpsData == null ? null : new GlobalPalStorageContainer()
            {
                Id = gpsData.ContainerId,
                // the first listed player should be the owner of the save file, and the save's owner has the global pal storage
                PlayerId = players.FirstOrDefault()?.PlayerId ?? parsed.Characters.FirstOrDefault(c => c.IsPlayer)?.PlayerId?.ToString() ?? "<NO-ID>",
            };

            var detectedContainers = CollectContainers(settings, parsed, players, dpsContainerByPlayerId.Values, gpsContainer);

            var result = new LevelSaveData()
            {
                Pals = [],
                Players = [],
                PalContainers = detectedContainers,
                Guilds = parsed.Groups,
                Bases = parsed.Bases.Select(b => new BaseInstance()
                {
                    Id = b.Id,
                    OwnerGuildId = b.OwnerGroupId.ToString(),
                    Container = detectedContainers.OfType<BasePalContainer>().FirstOrDefault(c => c.Id == b.ContainerId.ToString()),
                    ViewingCages = detectedContainers.OfType<ViewingCageContainer>().Where(c => c.BaseId == b.Id).ToList(),

                    Position = new WorldCoord()
                    {
                        X = b.Position.x,
                        Y = b.Position.y,
                        Z = b.Position.z,
                    },
                }).ToList()
            };

            var containerTypeById = detectedContainers.ToDictionary(c => c.Id, c => c.Type);

            foreach (var gvasInstance in parsed.Characters)
            {
                if (gvasInstance.IsPlayer)
                {
                    if (result.Players.Any(p => p.PlayerId == gvasInstance.PlayerId.ToString()))
                    {
                        logger.Warning("Found duplicate character instance for Player '{name}', skipping", gvasInstance.NickName);
                        continue;
                    }

                    result.Players.Add(new PlayerInstance()
                    {
                        PlayerId = gvasInstance.PlayerId.ToString(),
                        InstanceId = gvasInstance.InstanceId.ToString(),
                        Name = gvasInstance.NickName,
                        Level = gvasInstance.Level,
                        PalboxContainerId = detectedContainers.OfType<PalboxPalContainer>().FirstOrDefault(c => c.PlayerId == gvasInstance.PlayerId.ToString())?.Id,
                        PartyContainerId = detectedContainers.OfType<PlayerPartyContainer>().FirstOrDefault(c => c.PlayerId == gvasInstance.PlayerId.ToString())?.Id,
                        DimensionalPalStorageContainerId = dpsContainerByPlayerId.GetValueOrDefault(gvasInstance.PlayerId.ToString())?.Id,
                    });
                }
                else
                {
                    if (!containerTypeById.ContainsKey(gvasInstance.ContainerId?.ToString()))
                    {
                        // Level.sav contains a list of all known containers, but there are some cases where a pal
                        // references a container ID not in this list. the cause is not known, but I've seen "effective"
                        // container sizes from 1 to 40. there's no clear answer to "where" this container is (or its
                        // pals), so we won't bother referencing it
                        //
                        // (might be due to butchered pals? https://github.com/tylercamp/palcalc/issues/12#issuecomment-2101688781)
                        logger.Warning("unrecognized pal container id '{id}', skipping", gvasInstance.ContainerId);
                        continue;
                    }

                    var container = parsed.ContainerContents.Single(c => c.Id == gvasInstance.ContainerId.ToString());
                    if (!container.Slots.Any(s => s.InstanceId == gvasInstance.InstanceId))
                    {
                        logger.Debug("pal instance data '{palId}' references container '{containerId}' but the container has no record of this pal, skipping", gvasInstance.InstanceId, container.Id);
                        continue;
                    }

                    var pal = gvasInstance.ToPalInstance(db, containerTypeById[gvasInstance.ContainerId.ToString()]);
                    if (pal != null)
                        result.Pals.Add(pal);
                }
            }

            result.Pals.AddRange(dpsData.SelectMany(d => d.Pals));
            if (gpsData != null)
                result.Pals.AddRange(gpsData.Pals);

            var validPlayerIds = result.Players.Select(p => p.PlayerId);
            var allPlayerIds = validPlayerIds
                .Concat(result.Guilds.SelectMany(g => g.MemberIds))
                .Concat(result.Pals.Where(p => p.Location.Type != LocationType.GlobalPalStorage).Select(p => p.OwnerPlayerId))
                .Distinct()
                .ToList();

            foreach (var p in allPlayerIds.Except(validPlayerIds).OrderBy(id => id).ZipWithIndex())
            {
                var (unknownId, idx) = p;
                result.Players.Add(new PlayerInstance()
                {
                    InstanceId = $"__UNKNOWNID_{unknownId}__",
                    Level = 1,
                    Name = $"Unknown ({unknownId[..8]}) (#{idx + 1})",
                    PlayerId = unknownId,
                    PalboxContainerId = detectedContainers.OfType<PalboxPalContainer>().FirstOrDefault(c => c.PlayerId == unknownId)?.Id,
                    PartyContainerId = detectedContainers.OfType<PlayerPartyContainer>().FirstOrDefault(c => c.PlayerId == unknownId)?.Id,
                    DimensionalPalStorageContainerId = null,
                });
            }

            // TODO - no handling yet for adding missing guilds for bases, missing bases for base containers, etc.

            var playersMissingGuilds = allPlayerIds.Except(result.Guilds.SelectMany(g => g.MemberIds)).ToList();

            if (playersMissingGuilds.Count > 0)
            {
                var unknownGuild = new GuildInstance();
                unknownGuild.Name = "(No Guild) (!)";
                unknownGuild.InternalName = "__NO_GUILD__";
                unknownGuild.MemberIds = playersMissingGuilds;
                unknownGuild.OwnerId = "__UNKNOWN_OWNER__";
                unknownGuild.Id = "__UNKNOWN_ID__";

                result.Guilds.Add(unknownGuild);
            }

            return result;
        }
    }
}
