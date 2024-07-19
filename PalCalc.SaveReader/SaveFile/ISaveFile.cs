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

        private bool? isValid = null;
        public bool IsValid => isValid ??= Exists && GvasFile.IsValidGvas(FilePath);

        public DateTime LastModified => File.GetLastWriteTime(FilePath);

        protected void VisitGvas(params IVisitor[] visitors)
        {
            CompressedSAV.WithDecompressedSave(FilePath, stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints, true))
                    GvasFile.FromFArchive(archiveReader, visitors);
            });
        }

        /// <summary>
        /// Parses the save file as GVAS with the provided visitors. 
        /// </summary>
        public void ParseGvas(params IVisitor[] visitors) => ParseGvas(true, visitors);

        /// <summary>
        /// Parses the save file as GVAS with the provided visitors, optionally preserving (and returning)
        /// the full, raw, parsed data.
        /// </summary>
        /// <param name="preserveValues"></param>
        /// <returns>A `GvasFile` with a populated `Properties` field (if `preserveValues = true`)</returns>
        public GvasFile ParseGvas(bool preserveValues, params IVisitor[] visitors)
        {
            GvasFile result = null;
            CompressedSAV.WithDecompressedSave(FilePath, stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints, preserveValues))
                    result = GvasFile.FromFArchive(archiveReader, visitors);
            });
            return result;
        }
    }
}
