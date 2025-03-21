using PalCalc.Model;
using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Virtual;
using PalCalc.SaveReader.SaveFile.Xbox;
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

        private bool MatchesPath(string fileFullName, string baseFullName)
        {
            var baseName = Path.GetFileNameWithoutExtension(baseFullName);
            var baseExt = Path.GetExtension(baseFullName);

            var fileName = Path.GetFileNameWithoutExtension(fileFullName);

            char[] allowedChars = ['-', '_', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];
            return (
                fileFullName.StartsWith(baseName) &&
                Path.GetExtension(fileFullName) == baseExt &&
                (
                    fileName == baseName ||
                    allowedChars.Contains(fileName[baseName.Length])
                )
            );
        }

        public StandardSaveGame(string basePath)
        {
            BasePath = basePath;

            Level = new LevelSaveFile(new FilteredFileSource(basePath, f => MatchesPath(f, "Level.sav")));
            LevelMeta = new LevelMetaSaveFile(new FilteredFileSource(basePath, f => MatchesPath(f, "LevelMeta.sav")));
            LocalData = new LocalDataSaveFile(new FilteredFileSource(basePath, f => MatchesPath(f, "LocalData.sav")));
            WorldOption = new WorldOptionSaveFile(new FilteredFileSource(basePath, f => MatchesPath(f, "WorldOption.sav")));

            var playersPath = Path.Join(basePath, "Players");
            if (Directory.Exists(playersPath))
                Players = Directory
                    .EnumerateFiles(playersPath, "*.sav")
                    .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("_dps"))
                    .Select(f => new PlayersSaveFile(new SingleFileSource(f)))
                    .ToList();
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
            Level.Exists ? Level.LastModified : DateTime.MinValue,
            LevelMeta.Exists ? LevelMeta.LastModified : DateTime.MinValue,
            LocalData.Exists ? LocalData.LastModified : DateTime.MinValue,
            WorldOption.Exists ? WorldOption.LastModified : DateTime.MinValue,
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

        private XboxSaveMonitor monitor;

        public XboxSaveGame(
            XboxWgsFolder wgsFolder,
            string saveId
        )
        {
            BasePath = wgsFolder.UserBasePath;
            GameId = saveId;

            // note: no UNC path check, these should only be created for normal e.g. LocalAppData paths
            IsLocal = true;

            var filesByType = wgsFolder
                .Entries
                .Where(e => e.FileName.StartsWith($"{saveId}-"))
                .GroupBy(f => f.FileName.Split('-')[1]).ToDictionary(g => g.Key, g => g.ToList());

            if (filesByType.ContainsKey("Level"))
            {
                Level = new LevelSaveFile(new XboxFileSource(wgsFolder, saveId, f => f.Split("-").First() == "Level"));
            }

            if (filesByType.ContainsKey("LevelMeta"))
            {
                LevelMeta = new LevelMetaSaveFile(new XboxFileSource(wgsFolder, saveId, f => f.Split("-").First() == "LevelMeta"));
            }

            if (filesByType.ContainsKey("LocalData"))
            {
                LocalData = new LocalDataSaveFile(new XboxFileSource(wgsFolder, saveId, f => f.Split("-").First() == "LocalData"));
            }

            if (filesByType.ContainsKey("WorldOption"))
            {
                WorldOption = new WorldOptionSaveFile(new XboxFileSource(wgsFolder, saveId, f => f.Split("-").First() == "WorldOption"));
            }

            Players = filesByType
                .GetValueOrElse("Players", new List<XboxWgsEntry>())
                .Select(f => new PlayersSaveFile(new XboxFileSource(wgsFolder, saveId, nameWithoutSaveId => f.FileName == $"{saveId}-{nameWithoutSaveId}")))
                .ToList();

            monitor = wgsFolder.Monitor.GetSaveMonitor(saveId);
            monitor.Updated += Monitor_Updated;
        }

        private void Monitor_Updated() => Updated?.Invoke(this);

        public void Dispose() => this.monitor.Updated -= Monitor_Updated;

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
