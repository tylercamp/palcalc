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
    public class SaveGame
    {
        public SaveGame(string basePath)
        {
            BasePath = basePath;

            Level = new LevelSaveFile(basePath);
            LevelMeta = new LevelMetaSaveFile(basePath);
            LocalData = new LocalDataSaveFile(basePath);
            WorldOption = new WorldOptionSaveFile(basePath);
        }

        public string BasePath { get; }
        public string FolderName => Path.GetFileName(BasePath);
        public DateTime LastModified => new List<DateTime>()
        {
            Directory.GetLastWriteTime(BasePath),
            Level.LastModified,
            LevelMeta.LastModified,
            LocalData.LastModified,
            WorldOption.LastModified,
        }.Max();

        // (don't check `WorldOption`, not present for linux-based server saves)
        public bool IsValid => Level.Exists && LevelMeta.Exists && LocalData.Exists;

        public LevelSaveFile Level { get; }
        public LevelMetaSaveFile LevelMeta { get; }
        public LocalDataSaveFile LocalData { get; }
        public WorldOptionSaveFile WorldOption { get; }

        public override string ToString() => FolderName;
    }
}
