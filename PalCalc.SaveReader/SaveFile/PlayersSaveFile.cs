using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.SaveFile.Support.Level;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class DimensionalPalStorageData
    {
        public string ContainerId { get; internal set; }
        public List<PalInstance> Pals { get; internal set; }
    }

    public class PlayerMeta
    {
        public string SaveFileId { get; set; }
        public string PlayerId { get; set; }
        public string InstanceId { get; set; }

        public string PartyContainerId { get; set; }
        public string PalboxContainerId { get; set; }
    }

    // Files in the `Players/` folder may also have a `_dps.sav` file, which stores the player's pals in Dimensional Pal Storage
    public class PlayersDpsSaveFile(IFileSource files) : ISaveFile(files)
    {
        public virtual List<GvasCharacterInstance> ReadRawCharacters()
        {
            var v = new DimensionalPalStorage_CharacterInstanceVisitor();
            ParseGvas(v);
            return v.Result;
        }

        public virtual DimensionalPalStorageData ReadPals(string containerId)
        {
            var db = PalDB.LoadEmbedded();
            var pals = ReadRawCharacters()
                .Select(c => c.ToPalInstance(db, LocationType.DimensionalPalStorage))
                .ZipWithIndex()
                .Select(p =>
                {
                    // (`_dps` file has a plain, fixed array of entries. SlotIndex info seems to be the original loc the pal was stored before
                    // it was moved to DPS - ignore it)
                    var (c, i) = p;
                    if (c == null) return null;

                    c.Location.Index = i;
                    c.Location.ContainerId = containerId;
                    return c;
                })
                .SkipNull()
                .ToList();

            return new()
            {
                ContainerId = containerId,
                Pals = pals,
            };
        }
    }

    public class PlayersSaveFile(IFileSource files, PlayersDpsSaveFile dpsFile) : ISaveFile(files)
    {
        private static ILogger logger = Log.ForContext<PlayersSaveFile>();

        private const string K_PLAYER_UID = ".IndividualId.PlayerUId";
        private const string K_INSTANCE_ID = ".IndividualId.InstanceId";
        private const string K_PARTY_CONTAINER_ID = ".OtomoCharacterContainerId.ID";
        private const string K_PALBOX_CONTAINER_ID = ".PalStorageContainerId.ID";

        public PlayersDpsSaveFile DimensionalPalStorageSaveFile => dpsFile;

        public virtual PlayerMeta ReadPlayerContent()
        {
            var dataVisitor = new ValueCollectingVisitor(".SaveData", isCaseSensitive: false, K_PLAYER_UID, K_INSTANCE_ID, K_PARTY_CONTAINER_ID, K_PALBOX_CONTAINER_ID);
            ParseGvas(dataVisitor);

            string[] allKeys = [K_PLAYER_UID, K_INSTANCE_ID, K_PARTY_CONTAINER_ID, K_PALBOX_CONTAINER_ID];

            var missingKeys = allKeys.Where(k => !dataVisitor.Result.ContainsKey(k));
            if (missingKeys.Any())
            {
                logger.Warning("Player save file from {FileSource} is missing required properties: {Keys}", FilePaths.ToList(), missingKeys);
                return null;
            }

            return new PlayerMeta
            {
                PlayerId = dataVisitor.Result[K_PLAYER_UID].ToString(),
                InstanceId = dataVisitor.Result[K_INSTANCE_ID].ToString(),
                PartyContainerId = dataVisitor.Result[K_PARTY_CONTAINER_ID].ToString(),
                PalboxContainerId = dataVisitor.Result[K_PALBOX_CONTAINER_ID].ToString(),
            };
        }
    }
}
