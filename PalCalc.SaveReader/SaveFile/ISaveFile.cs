using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public abstract class ISaveFile(IFileSource files)
    {
        // ("Save Files" can sometimes be split across several actual files on disk)
        public IEnumerable<string> FilePaths => files.Content.ToList();

        public bool Exists => files.Content.Any(File.Exists);

        private bool? isValid = null;
        public bool IsValid => isValid ??= Exists && GvasFile.IsValidGvas(files);

        public DateTime LastModified => files.Content.Select(File.GetLastWriteTime).Max();

        protected virtual void VisitGvas(params IVisitor[] visitors)
        {
            CompressedSAV.WithDecompressedSave(files, stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints, false))
                    GvasFile.FromFArchive(archiveReader, visitors);
            });
        }

        /// <summary>
        /// Parses the save file as GVAS with the provided visitors. 
        /// </summary>
        public void ParseGvas(params IVisitor[] visitors) => ParseGvas(false, visitors);

        /// <summary>
        /// Parses the save file as GVAS with the provided visitors, optionally preserving (and returning)
        /// the full, raw, parsed data.
        /// </summary>
        /// <param name="preserveValues"></param>
        /// <returns>A `GvasFile` with a populated `Properties` field (if `preserveValues = true`)</returns>
        public virtual GvasFile ParseGvas(bool preserveValues, params IVisitor[] visitors)
        {
            GvasFile result = null;
            CompressedSAV.WithDecompressedSave(files, stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints, preserveValues))
                    result = GvasFile.FromFArchive(archiveReader, visitors);
            });
            return result;
        }
    }
}
