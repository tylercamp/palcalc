using PalCalc.SaveReader.FArchive;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class PlayerMeta
    {
        public string PlayerId { get; set; }
        public string InstanceId { get; set; }

        public string PartyContainerId { get; set; }
        public string PalboxContainerId { get; set; }
    }

    public class PlayersSaveFile(IFileSource files) : ISaveFile(files)
    {
        private static ILogger logger = Log.ForContext<PlayersSaveFile>();

        private const string K_PLAYER_UID = ".IndividualId.PlayerUId";
        private const string K_INSTANCE_ID = ".IndividualId.InstanceId";
        private const string K_PARTY_CONTAINER_ID = ".OtomoCharacterContainerId.ID";
        private const string K_PALBOX_CONTAINER_ID = ".PalStorageContainerId.ID";

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
