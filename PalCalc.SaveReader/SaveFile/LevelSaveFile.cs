using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.SaveFile.Support.Level;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class LevelSaveData
    {
        public List<PalInstance> Pals { get; set; }
        public List<PlayerInstance> Players { get; set; }
        public List<GuildInstance> Guilds { get; set; }
    }

    public class LevelSaveFile : ISaveFile
    {
        private static ILogger logger = Log.ForContext<LevelSaveFile>();

        public LevelSaveFile(string folderPath) : base(folderPath) { }

        public override string FileName => "Level.sav";

        private Guid MostCommonOwner(PalContainer container) => container.Slots.GroupBy(s => s.PlayerId).MaxBy(g => g.Count()).Key;
        
        public LevelSaveData ReadCharacterData(PalDB db)
        {
            logger.Debug("parsing content");

            var containerVisitor = new PalContainerVisitor();
            var instanceVisitor = new CharacterInstanceVisitor();
            var groupVisitor = new GroupVisitor();
            VisitGvas(containerVisitor, instanceVisitor, groupVisitor);

            logger.Debug("processing data");

            // note: you can read `Players/...sav` and fetch ".SaveData.PalStorageContainerId.ID" to see exactly
            //       which pal box is for which player, but will just infer this from `Level.sav` via the most
            //       common pal owner ID. makes support easier, don't need to provide extra files

            var containersById = containerVisitor.CollectedContainers.ToDictionary(c => c.Id);
            var containerOwners = containerVisitor.CollectedContainers.ToDictionary(c => c.Id, MostCommonOwner);
            var palBoxesByPlayerId = containerVisitor.CollectedContainers.GroupBy(MostCommonOwner).ToDictionary(g => g.Key, g => g.MaxBy(c => c.MaxEntries));

            //var containersByPlayer = containerVisitor.CollectedContainers.GroupBy(c => containerOwners[c.Id]).ToDictionary(g => g.Key, g => g.ToList());

            var containerTypeById = containerVisitor.CollectedContainers.ToDictionary(c => c.Id, c =>
            {
                if (c.MaxEntries == GameConstants.PlayerPartySize) return LocationType.PlayerParty;

                var palBoxForPlayer = palBoxesByPlayerId[containerOwners[c.Id]];
                if (c.Id == palBoxForPlayer.Id) return LocationType.Palbox;

                return LocationType.Base;
            });

            var result = new LevelSaveData()
            {
                Pals = new List<PalInstance>(),
                Players = new List<PlayerInstance>(),
                Guilds = groupVisitor.Result,
            };

            foreach (var gvasInstance in instanceVisitor.Result)
            {
                if (gvasInstance.IsPlayer)
                {
                    result.Players.Add(new PlayerInstance()
                    {
                        PlayerId = gvasInstance.PlayerId.ToString(),
                        InstanceId = gvasInstance.InstanceId.ToString(),
                        Name = gvasInstance.NickName,
                        Level = gvasInstance.Level,
                    });
                }
                else
                {
                    var sanitizedCharId = gvasInstance.CharacterId.Replace("Boss_", "", StringComparison.InvariantCultureIgnoreCase);
                    var pal = db.Pals.FirstOrDefault(p => p.InternalName.ToLower() == sanitizedCharId.ToLower());

                    if (pal == null)
                    {
                        // skip unrecognized pals
                        logger.Warning("unrecognized pal '{name}', skipping", sanitizedCharId);
                        continue;
                    }

                    var traits = gvasInstance.Traits
                        .Select(name =>
                        {
                            var trait = db.Traits.FirstOrDefault(t => t.InternalName == name);
                            if (trait == null)
                            {
                                logger.Warning("unrecognized trait '{internalName}', skipping", name);
                            }
                            return trait ?? new UnrecognizedTrait(name);
                        })
                        .ToList();

                    result.Pals.Add(new PalInstance()
                    {
                        Pal = pal,
                        InstanceId = gvasInstance.InstanceId.ToString(),
                        OwnerPlayerId = gvasInstance.OwnerPlayerId?.ToString() ?? gvasInstance.OldOwnerPlayerIds.First().ToString(),

                        Level = gvasInstance.Level,
                        NickName = gvasInstance.NickName,
                        Gender = gvasInstance.Gender.Contains("Female") ? PalGender.FEMALE : PalGender.MALE,
                        Traits = traits,
                        Location = new PalLocation()
                        {
                            Type = containerTypeById[gvasInstance.ContainerId.ToString()],
                            Index = gvasInstance.SlotIndex,
                        }
                    });
                }
            }

            logger.Debug("done");
            return result;
        }
    }
}
