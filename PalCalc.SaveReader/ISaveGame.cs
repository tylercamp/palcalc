using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Virtual;
using System.Net;


namespace PalCalc.SaveReader
{
    public interface ISaveGame : IDisposable
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

        // Flag to indicate if the save game is on the lcoal file system
        bool IsLocal { get; }

        event Action<ISaveGame> Updated;
    }

    public class StandardSaveGame : ISaveGame
    {
        private FileSystemWatcher folderWatcher;
        public event Action<ISaveGame> Updated;

        private string[] ResolvePaths(string basePath)
        {
            // when a save file is split across multiple files, detect those and return the appropriate paths
            // (usually only happens when an Xbox save is exported)

            var baseName = Path.GetFileNameWithoutExtension(basePath);
            var baseExt = Path.GetExtension(basePath);

            var matchingFiles = Directory
                .EnumerateFiles(Path.GetDirectoryName(basePath))
                .Where(p => Path.GetFileName(p).StartsWith(baseName))
                .Where(p => Path.GetExtension(p) == baseExt)
                .Where(p =>
                {
                    // (prevent `Level.sav` from matching `LevelMeta.sav`)
                    var name = Path.GetFileNameWithoutExtension(p);
                    if (name == baseName) return true;

                    char[] allowedChars = ['-', '_', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

                    return allowedChars.Contains(name[baseName.Length]);
                })
                .OrderBy(Path.GetFileNameWithoutExtension)
                .ToArray();

            if (matchingFiles.Length == 0) return [basePath];
            else return matchingFiles;
        }

        public StandardSaveGame(string basePath)
        {
            BasePath = basePath;

            Level = new LevelSaveFile(ResolvePaths(Path.Join(basePath, "Level.sav")));
            LevelMeta = new LevelMetaSaveFile(ResolvePaths(Path.Join(basePath, "LevelMeta.sav")));
            LocalData = new LocalDataSaveFile(ResolvePaths(Path.Join(basePath, "LocalData.sav")));
            WorldOption = new WorldOptionSaveFile(ResolvePaths(Path.Join(basePath, "WorldOption.sav")));

            var playersPath = Path.Join(basePath, "Players");
            if (Directory.Exists(playersPath))
                Players = Directory.EnumerateFiles(playersPath, "*.sav").Select(f => new PlayersSaveFile([f])).ToList();
            else
                Players = new List<PlayersSaveFile>();

            if (Directory.Exists(basePath))
            {
                IsLocal = true;
                if (new Uri(basePath).IsUnc)
                {
                    try
                    {
                        IPAddress[] host;
                        // get host addresses
                        host = Dns.GetHostAddresses(basePath);
                        // get local addresses
                        IPAddress[] local = Dns.GetHostAddresses(Dns.GetHostName());
                        // check if local
                        if (!host.Any(hostAddress => IPAddress.IsLoopback(hostAddress) || local.Contains(hostAddress)))
                        {
                            IsLocal = false;
                        }
                    }
                    catch (Exception)
                    {
                        IsLocal = false;
                    }
                }
                

                folderWatcher = new FileSystemWatcher(basePath);
                folderWatcher.Changed += FolderWatcher_Updated;
                folderWatcher.Created += FolderWatcher_Updated;
                folderWatcher.Deleted += FolderWatcher_Updated;
                folderWatcher.Renamed += FolderWatcher_Updated;

                folderWatcher.IncludeSubdirectories = true;
                folderWatcher.EnableRaisingEvents = true;
            }
            else
            {
                IsLocal = false;
            }
        }

        public void Dispose()
        {
            if (folderWatcher != null)
            {
                folderWatcher.Changed -= FolderWatcher_Updated;
                folderWatcher.Created -= FolderWatcher_Updated;
                folderWatcher.Deleted -= FolderWatcher_Updated;
                folderWatcher.Renamed -= FolderWatcher_Updated;
                folderWatcher.Dispose();

                folderWatcher = null;
            }
        }

        private void FolderWatcher_Updated(object sender, FileSystemEventArgs e)
        {
            Updated?.Invoke(this);
        }

        public string BasePath { get; private set; }

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

        public bool IsLocal { get; private set; }

        public override string ToString() => FolderName;
    }

    public class XboxSaveGame : ISaveGame
    {
        public event Action<ISaveGame> Updated;

        private List<FileSystemWatcher> fileWatchers;

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

            if (Directory.Exists(userBasePath))
            {
                IsLocal = true;
                if (new Uri(userBasePath).IsUnc)
                {
                    try
                    {
                        IPAddress[] host;
                        // get host addresses
                        host = Dns.GetHostAddresses(userBasePath);
                        // get local addresses
                        IPAddress[] local = Dns.GetHostAddresses(Dns.GetHostName());
                        // check if local
                        if (!host.Any(hostAddress => IPAddress.IsLoopback(hostAddress) || local.Contains(hostAddress)))
                        {
                            IsLocal = false;
                        }
                    }
                    catch (Exception)
                    {
                        IsLocal = false;
                    }
                }
            }
            else
            {
                IsLocal = false;
            }

            this.fileWatchers = fileWatchers.ToList();

            foreach (var watcher in this.fileWatchers)
            {
                watcher.Changed += Watcher_Updated;
                watcher.Created += Watcher_Updated;
                watcher.Deleted += Watcher_Updated;
                watcher.Renamed += Watcher_Updated;
            }
        }

        public void Dispose()
        {
            if (fileWatchers != null)
            {
                foreach (var watcher in fileWatchers)
                {
                    watcher.Changed -= Watcher_Updated;
                    watcher.Created -= Watcher_Updated;
                    watcher.Deleted -= Watcher_Updated;
                    watcher.Renamed -= Watcher_Updated;
                    watcher.Dispose();
                }

                fileWatchers = null;
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

        public bool IsLocal { get; private set; }
    }

    /// <summary>
    /// A fake save-game whose ISaveFiles don't return any data. Meant to be a placeholder for "fake" saves added in Pal Calc.
    /// </summary>
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

        public void Dispose()
        {
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

        public bool IsLocal => true;

        public event Action<ISaveGame> Updated;
    }
}
