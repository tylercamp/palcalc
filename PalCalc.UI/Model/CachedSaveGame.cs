using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    public class CachedSaveGame
    {
        public CachedSaveGame(ISaveGame underlyingSave)
        {
            UnderlyingSave = underlyingSave;
        }

        public DateTime LastModified { get; set; }

        public bool IsServerSave { get; set; }

        public string WorldName { get; set; }
        public string PlayerName { get; set; }
        public int? PlayerLevel { get; set; }
        public int InGameDay { get; set; }

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

        public bool IsOutdated => LastModified != UnderlyingSave.LastModified;

        public string StateId => $"{IdentifierFor(UnderlyingSave)}-{LastModified.Ticks}";

        public static event Action<ISaveGame> SaveFileLoadStart;
        public static event Action<ISaveGame> SaveFileLoadEnd;
        public static event Action<ISaveGame, Exception> SaveFileLoadError;

        public static string IdentifierFor(ISaveGame game)
        {
            return $"{game.UserId}-{game.GameId}";
        }

        public static CachedSaveGame FromSaveGame(ISaveGame game, PalDB db)
        {
            SaveFileLoadStart?.Invoke(game);

            CachedSaveGame result;
#if HANDLE_ERRORS
            try
            {
#endif
                GameMeta meta = null;
                // `LevelMeta` is sometimes unavailable for Xbox saves, which shouldn't prevent us from
                // being able to load the data
                try { meta = game.LevelMeta.ReadGameOptions(); } catch { }
                
                var charData = game.Level.ReadCharacterData(db);
                result = new CachedSaveGame(game)
                {
                    LastModified = game.LastModified,
                    OwnedPals = charData.Pals,
                    Guilds = charData.Guilds,
                    Players = charData.Players,
                    PlayerLevel = meta?.PlayerLevel,
                    PlayerName = meta?.PlayerName ?? "UNKNOWN",
                    WorldName = meta?.WorldName ?? "UNKNOWN WORLD",
                    InGameDay = meta?.InGameDay ?? 0,
                };
#if HANDLE_ERRORS
            }
            catch (Exception ex)
            {
                SaveFileLoadError?.Invoke(game, ex);
                return null;
            }
#endif

            SaveFileLoadEnd?.Invoke(game);

            return result;
        }

        public string ToJson(PalDB db) => JsonConvert.SerializeObject(this, new PalInstanceJsonConverter(db));

        public static CachedSaveGame FromJson(string json, PalDB db) => JsonConvert.DeserializeObject<CachedSaveGame>(json, new PalInstanceJsonConverter(db));

    }
}
