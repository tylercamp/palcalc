using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public abstract class ISaveFile
    {
        private string basePath;
        public ISaveFile(string folderPath)
        {
            basePath = folderPath;
        }

        public abstract string FileName { get; }

        public string FilePath => Path.Join(basePath, FileName);
        public bool Exists => File.Exists(FilePath);
        public DateTime LastModified => File.GetLastWriteTime(FilePath);

        protected void VisitGvas(params IVisitor[] visitors)
        {
            CompressedSAV.WithDecompressedSave(FilePath, stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
                    GvasFile.FromFArchive(archiveReader, visitors);
            });
        }

        public GvasFile ParseGvas(params IVisitor[] visitors)
        {
            GvasFile result = null;
            CompressedSAV.WithDecompressedSave(FilePath, stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
                    result = GvasFile.FromFArchive(archiveReader, visitors);
            });
            return result;
        }
    }
}
