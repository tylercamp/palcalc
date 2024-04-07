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
    //public class SaveFile
    //{
    //    public SaveFile(string basePath, string fileName)
    //    {
    //        FullPath = $"{basePath}/{fileName}";
    //    }

    //    public bool Exists => File.Exists(FullPath);

    //    public string FullPath { get; }
    //    public GvasFile ParsedGvas(IEnumerable<IVisitor> visitors)
    //    {
    //        GvasFile result = null;
    //        CompressedSAV.WithDecompressedSave(FullPath, stream =>
    //        {
    //            using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
    //                result = GvasFile.FromFArchive(archiveReader, visitors);
    //        });
    //        return result;
    //    }
    //}

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

        public bool IsValid => Level.Exists && LevelMeta.Exists && LocalData.Exists && WorldOption.Exists;

        public LevelSaveFile Level { get; }
        public LevelMetaSaveFile LevelMeta { get; }
        public LocalDataSaveFile LocalData { get; }
        public WorldOptionSaveFile WorldOption { get; }
    }
}
