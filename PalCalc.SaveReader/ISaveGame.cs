using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Virtual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.PlayTo;

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

        event Action<ISaveGame> Updated;
    }

    public class StandardSaveGame : ISaveGame
    {
        private FileSystemWatcher folderWatcher;
        public event Action<ISaveGame> Updated;

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

            if (Directory.Exists(basePath))
            {
                folderWatcher = new FileSystemWatcher(basePath);
                folderWatcher.Changed += FolderWatcher_Updated;
                folderWatcher.Created += FolderWatcher_Updated;
                folderWatcher.Deleted += FolderWatcher_Updated;
                folderWatcher.Renamed += FolderWatcher_Updated;

                folderWatcher.IncludeSubdirectories = true;
                folderWatcher.EnableRaisingEvents = true;
            }
        }

        private void FolderWatcher_Updated(object sender, FileSystemEventArgs e)
        {
            Updated?.Invoke(this);
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

        public bool IsValid => Level != null && Level.IsValid;

        public override string ToString() => FolderName;
    }

    public class XboxSaveGame : ISaveGame
    {
        public event Action<ISaveGame> Updated;

        public XboxSaveGame(
            string userBasePath,
            string saveId,
            LevelSaveFile level,
            LevelMetaSaveFile levelMeta,
            LocalDataSaveFile localData,
            WorldOptionSaveFile worldOption,
            List<PlayersSaveFile> players,
            IEnumerable<FileSystemWatcher> fileWatchers
        )
        {
            BasePath = userBasePath;
            GameId = saveId;
            Level = level;
            LevelMeta = levelMeta;
            LocalData = localData;
            WorldOption = worldOption;
            Players = players;

            foreach (var watcher in fileWatchers)
            {
                watcher.Changed += Watcher_Updated;
                watcher.Created += Watcher_Updated;
                watcher.Deleted += Watcher_Updated;
                watcher.Renamed += Watcher_Updated;
            }
        }

        private void Watcher_Updated(object sender, FileSystemEventArgs e)
        {
            Updated?.Invoke(this);
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

    public class VirtualSaveGame : ISaveGame
    {
        public VirtualSaveGame(
            string userId,
            string gameId,
            VirtualLevelSaveFile level,
            VirtualLevelMetaSaveFile levelMeta,
            VirtualLocalDataSaveFile localData,
            VirtualWorldOptionSaveFile worldOption
        )
        {
            Level = level;
            LevelMeta = levelMeta;
            LocalData = localData;
            WorldOption = worldOption;
            Players = [];

            UserId = userId;
            GameId = gameId;
        }

        public string BasePath => null;

        public string UserId { get; }

        public string GameId { get; }

        public DateTime LastModified { get; set; } = DateTime.Now;

        public LevelSaveFile Level { get; }

        public LevelMetaSaveFile LevelMeta { get; }

        public LocalDataSaveFile LocalData { get; }

        public WorldOptionSaveFile WorldOption { get; }

        public List<PlayersSaveFile> Players { get; }

        public bool IsValid => true;

        public event Action<ISaveGame> Updated;
    }
}
