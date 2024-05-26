using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model
{
    internal class SaveGameDetails
    {
        private static readonly string SaveReaderVersion = "v2";

        public SaveGameDetails(ISaveGame underlyingSave)
        {
            UnderlyingSave = underlyingSave;
            ReaderVersion = SaveReaderVersion;
        }

        public DateTime LastModified { get; set; }

        public string DatabaseVersion { get; set; }
        public string ReaderVersion { get; set; }

        public List<PlayerInstance> Players { get; set; }
        public List<GuildInstance> Guilds { get; set; }
        public List<PalInstance> OwnedPals { get; set; }

        private Dictionary<string, PlayerInstance> playersByName;
        public Dictionary<string, PlayerInstance> PlayersById =>
            playersByName ??= Players.ToDictionary(p => p.PlayerId);

        public Dictionary<string, PlayerInstance> PlayersByInstanceId =>
            playersByName ??= Players.ToDictionary(p => p.InstanceId);

        private Dictionary<string, GuildInstance> playerGuilds;
        public Dictionary<string, GuildInstance> GuildsByPlayerId =>
            playerGuilds ??= Players.ToDictionary(p => p.PlayerId, p => Guilds.FirstOrDefault(g => g.MemberIds.Contains(p.PlayerId)));

        [JsonIgnore]
        public ISaveGame UnderlyingSave { get; set; }

        public bool IsValid => UnderlyingSave.IsValid;

        public bool IsOutdated(PalDB currentDb) => LastModified != UnderlyingSave.LastModified || DatabaseVersion != currentDb.Version || ReaderVersion != SaveReaderVersion;

        public string StateId => $"{UnderlyingSave.Identifier()}-{LastModified.Ticks}";

        public static SaveGameDetails FromSaveGame(ISaveGame game, PalDB db)
        {
            SaveGameDetails result;

            var charData = game.Level.ReadCharacterData(db);
            result = new SaveGameDetails(game)
            {
                DatabaseVersion = db.Version,
                LastModified = game.LastModified,
                OwnedPals = charData.Pals,
                Guilds = charData.Guilds,
                Players = charData.Players,
            };

            return result;
        }
    }
}
