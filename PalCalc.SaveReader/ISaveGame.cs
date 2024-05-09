using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using PalCalc.SaveReader.SaveFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    public interface ISaveGame
    {
        string BasePath { get; }

        string UserId { get; } // generally based on the parent ISavesLocation
        string GameId { get; }

        DateTime LastModified { get; }

        LevelSaveFile Level { get; }
        LevelMetaSaveFile LevelMeta { get; }
        LocalDataSaveFile LocalData { get; }
        WorldOptionSaveFile WorldOption { get; }

        List<PlayersSaveFile> Players { get; }

        // (don't check `WorldOption`, not present for linux-based server saves)
        bool IsValid { get; }
    }

    public class StandardSaveGame : ISaveGame
    {
        public StandardSaveGame(string basePath)
        {
            BasePath = basePath;

            Level = new LevelSaveFile(Path.Join(basePath, "Level.sav"));
            LevelMeta = new LevelMetaSaveFile(Path.Join(basePath, "LevelMeta.sav"));
            LocalData = new LocalDataSaveFile(Path.Join(basePath, "LocalData.sav"));
            WorldOption = new WorldOptionSaveFile(Path.Join(basePath, "WorldOption.sav"));

            var playersPath = Path.Join(basePath, "Players");
            if (Directory.Exists(playersPath))
                Players = Directory.EnumerateFiles(playersPath, "*.sav").Select(f => new PlayersSaveFile(f)).ToList();
            else
                Players = new List<PlayersSaveFile>();
        }

        public string BasePath { get; }

        public string UserId => Path.GetFileName(Path.GetDirectoryName(BasePath));
        public string GameId => Path.GetFileName(BasePath);

        public string FolderName => Path.GetFileName(BasePath);
        public DateTime LastModified => new List<DateTime>()
        {
            Directory.GetLastWriteTime(BasePath),
            Level.LastModified,
            LevelMeta.LastModified,
            LocalData.LastModified,
            WorldOption.LastModified,
        }.Concat(Players.Select(p => p.LastModified)).Max();

        public LevelSaveFile Level { get; }
        public LevelMetaSaveFile LevelMeta { get; }
        public LocalDataSaveFile LocalData { get; }
        public WorldOptionSaveFile WorldOption { get; }
        public List<PlayersSaveFile> Players { get; }

        public bool IsValid =>
            Level != null && Level.IsValid &&
            LevelMeta != null && LevelMeta.IsValid;

        public override string ToString() => FolderName;
    }

    public class XboxSaveGame : ISaveGame
    {
        public XboxSaveGame(
            string userBasePath,
            string saveId,
            LevelSaveFile level,
            LevelMetaSaveFile levelMeta,
            LocalDataSaveFile localData,
            WorldOptionSaveFile worldOption,
            List<PlayersSaveFile> players
        )
        {
            BasePath = userBasePath;
            GameId = saveId;
            Level = level;
            LevelMeta = levelMeta;
            LocalData = localData;
            WorldOption = worldOption;
            Players = players;
        }

        public string BasePath { get; }

        public string UserId => Path.GetFileName(BasePath);
        public string GameId { get; }

        public DateTime LastModified => new ISaveFile[] { Level, LevelMeta, LocalData, WorldOption }.Concat(Players).Max(s => s?.LastModified ?? DateTime.MinValue);

        public LevelSaveFile Level { get; }
        public LevelMetaSaveFile LevelMeta { get; }
        public LocalDataSaveFile LocalData { get; }
        public WorldOptionSaveFile WorldOption { get; }
        public List<PlayersSaveFile> Players { get; }

        public bool IsValid =>
            Level != null && Level.IsValid; // don't check for `LevelMeta` - may be temporarily missing for files with "wrapper" header
    }
}
