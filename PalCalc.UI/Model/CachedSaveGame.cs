﻿using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.UI.Localization;
using PalCalc.UI.View;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PalCalc.UI.Model
{
    public class CachedSaveGame
    {
        private static readonly string SaveReaderVersion = "v38";

        public CachedSaveGame(ISaveGame underlyingSave)
        {
            UnderlyingSave = underlyingSave;
            ReaderVersion = SaveReaderVersion;
            StateId = 0;
        }

        [JsonProperty]
        public DateTime LastModified { get; private set; }

        [JsonProperty]
        public bool IsServerSave { get; private set; }

        [JsonProperty]
        public string WorldName { get; private set; }
        [JsonProperty]
        public string PlayerName { get; private set; }
        [JsonProperty]
        public int? PlayerLevel { get; private set; }
        [JsonProperty]
        public int InGameDay { get; private set; }

        [JsonProperty]
        public string DatabaseVersion { get; private set; }
        [JsonProperty]
        public string ReaderVersion { get; private set; }

        [JsonProperty]
        public List<PlayerInstance> Players { get; private set; }
        [JsonProperty]
        public List<GuildInstance> Guilds { get; private set; }
        [JsonProperty]
        public List<PalInstance> OwnedPals { get; private set; }

        // note: `OwnedPals` is the primary source of pal info, `Bases` and `PalContainers` are
        //       just used for supplemental info like which bases belong to which guild, which
        //       viewing cages belong to bases, world coordinates of bases, etc.

        [JsonProperty]
        public List<BaseInstance> Bases { get; private set; }
        [JsonProperty]
        public List<IPalContainer> PalContainers { get; private set; }

        // (more accurate name would be "NumRefreshes")
        [JsonIgnore]
        public int StateId { get; private set; }

        private Dictionary<string, PlayerInstance> playersByName;
        [JsonIgnore]
        public Dictionary<string, PlayerInstance> PlayersById =>
            playersByName ??= Players.ToDictionary(p => p.PlayerId);

        private Dictionary<string, GuildInstance> playerGuilds;
        [JsonIgnore]
        public Dictionary<string, GuildInstance> GuildsByPlayerId =>
            playerGuilds ??= Players.ToDictionary(p => p.PlayerId, p => Guilds.FirstOrDefault(g => g.MemberIds.Contains(p.PlayerId)));

        private Dictionary<string, List<BaseInstance>> basesByGuild;
        [JsonIgnore]
        public Dictionary<string, List<BaseInstance>> BasesByGuildId =>
            basesByGuild ??= Guilds.Select(g => g.Id).Concat(Bases.Select(b => b.OwnerGuildId)).Distinct().ToDictionary(g => g, g => Bases.Where(b => b.OwnerGuildId == g).ToList());

        private Dictionary<string, GuildInstance> containerGuilds;
        [JsonIgnore]
        public Dictionary<string, GuildInstance> GuildsByContainerId =>
            containerGuilds ??= PalContainers?.ToDictionary(
                c => c.Id,
                c => c switch
                {
                    PalboxPalContainer pbc => GuildsByPlayerId.GetValueOrDefault(pbc.PlayerId),
                    PlayerPartyContainer ppc => GuildsByPlayerId.GetValueOrDefault(ppc.PlayerId),
                    BasePalContainer bpc => Guilds.FirstOrDefault(g => g.Id == Bases.FirstOrDefault(b => b.Id == bpc.BaseId)?.OwnerGuildId),
                    ViewingCageContainer vcc => Guilds.FirstOrDefault(g => g.Id == Bases.FirstOrDefault(b => b.Id == vcc.BaseId)?.OwnerGuildId),
                    DimensionalPalStorageContainer dpsc => GuildsByPlayerId.GetValueOrDefault(dpsc.PlayerId),
                    _ => null
                }
            );

        // NOTE: Any new fields should be added here
        public void CopyFrom(CachedSaveGame src)
        {
            if (UnderlyingSave != src.UnderlyingSave) throw new InvalidOperationException();

            LastModified = src.LastModified;
            IsServerSave = src.IsServerSave;
            WorldName = src.WorldName;
            PlayerName = src.PlayerName;
            PlayerLevel = src.PlayerLevel;
            InGameDay = src.InGameDay;
            DatabaseVersion = src.DatabaseVersion;
            ReaderVersion = src.ReaderVersion;
            PalContainers = [.. src.PalContainers];
            Players = [.. src.Players];
            Guilds = [.. src.Guilds];
            OwnedPals = [.. src.OwnedPals];
            Bases = [.. src.Bases];

            playersByName = null;
            playerGuilds = null;
            basesByGuild = null;

            StateId += 1;
        }

        [JsonIgnore]
        public ISaveGame UnderlyingSave { get; internal set; }

        public bool IsValid => UnderlyingSave.IsValid;

        public bool IsOutdated(PalDB currentDb) => LastModified != UnderlyingSave.LastModified || DatabaseVersion != currentDb.Version || ReaderVersion != SaveReaderVersion;

        public static event Action<ISaveGame> SaveFileLoadStart;
        public static event Action<ISaveGame, CachedSaveGame> SaveFileLoadEnd;
        public static event Action<ISaveGame, Exception> SaveFileLoadError;

        public static string IdentifierFor(ISaveGame game)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();

            foreach (var c in $"{game.UserId}-{game.GameId}")
                if (!invalidChars.Contains(c)) sb.Append(c);

            return sb.ToString();
        }

        private static CachedSaveGame sampleForDesignerView;
        public static CachedSaveGame SampleForDesignerView =>
            sampleForDesignerView ??=
                DirectSavesLocation.AllLocal
                    .SelectMany(l => l.ValidSaveGames)
                    .OrderByDescending(g => g.LevelMeta.ReadGameOptions().PlayerLevel)
                    .Select(g => FromSaveGame(null, g, PalDB.LoadEmbedded(), GameSettings.Defaults))
                    .First();

        public static CachedSaveGame FromSaveGame(ISavesLocation containerLocation, ISaveGame game, PalDB db, GameSettings settings)
        {
            // save-load may start from anywhere, and Dispatcher.Current isn't guaranteed to
            // match the UI thread
            if (Application.Current.Dispatcher.Thread != Thread.CurrentThread)
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    return FromSaveGame(containerLocation, game, db, settings);
                });
            }

            var loadingModal = new LoadingSaveFileModal();
            loadingModal.DataContext = LocalizationCodes.LC_SAVE_FILE_RELOADING.Bind();

            var isDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
            if (!isDesignMode)
            {
                loadingModal.Owner = Application.Current.MainWindow;
                SaveFileLoadStart?.Invoke(game);
            }

            CachedSaveGame result = PCDebug.HandleErrors(
                action: () =>
                {
                    return loadingModal.ShowDialogDuring(() =>
                    {
                        GameMeta meta = null;
                        // `LevelMeta` is sometimes unavailable for Xbox saves, which shouldn't prevent us from
                        // being able to load the data
                        try { meta = game.LevelMeta.ReadGameOptions(); } catch { }

                        var charData = game.Level.ReadCharacterData(db, settings, game.Players, containerLocation?.GlobalPalStorage);

                        return new CachedSaveGame(game)
                        {
                            DatabaseVersion = db.Version,
                            LastModified = game.LastModified,
                            OwnedPals = charData.Pals,
                            Guilds = charData.Guilds,
                            Players = charData.Players,
                            Bases = charData.Bases,
                            PalContainers = charData.PalContainers,
                            PlayerLevel = meta?.PlayerLevel,
                            PlayerName = meta?.PlayerName ?? "UNKNOWN",
                            WorldName = meta?.WorldName ?? "UNKNOWN WORLD",
                            InGameDay = meta?.InGameDay ?? 0,
                        };
                    });
                },
                handleErr: (ex) =>
                {
                    SaveFileLoadError?.Invoke(game, ex);
                    return null;
                }
            );

            if (result != null && !isDesignMode) SaveFileLoadEnd?.Invoke(game, result);

            return result;
        }

        private static JsonConverter<IPalContainer> palContainerConverter = new PalContainerJsonConverter();
        public string ToJson(PalDB db) => JsonConvert.SerializeObject(this, new PalInstanceJsonConverter(db), palContainerConverter);

        public static CachedSaveGame FromJson(string json, PalDB db) => JsonConvert.DeserializeObject<CachedSaveGame>(json, new PalInstanceJsonConverter(db), palContainerConverter);
    }
}
