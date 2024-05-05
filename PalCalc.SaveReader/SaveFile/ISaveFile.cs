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
        public ISaveFile(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }
        public bool Exists => File.Exists(FilePath);
        public bool IsValid
        {
            get
            {
                if (!Exists) return false;
                if (!CompressedSAV.IsValidSave(FilePath)) return false;

                return GvasFile.IsValidGvas(FilePath);

                //var isValidSave = false;
                //CompressedSAV.WithDecompressedSave(FilePath, stream =>
                //{
                //    using (var reader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
                //        isValidSave = GvasFile.IsValidGvas(reader);
                //});

                //return isValidSave;
            }
        }

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
