using PalCalc.SaveReader.FArchive;
using PalCalc.SaveReader.GVAS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Xbox
{
    public class AggregateLevelSaveFile(LevelSaveFile[] sourceSaves) : LevelSaveFile(sourceSaves[0].FilePath)
    {
        protected override void VisitGvas(params IVisitor[] visitors)
        {
            CompressedSAV.WithDecompressedAggregateSave(sourceSaves.Select(f => f.FilePath), stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints, false))
                    GvasFile.FromFArchive(archiveReader, visitors);
            });
        }

        public override GvasFile ParseGvas(bool preserveValues, params IVisitor[] visitors)
        {
            GvasFile result = null;
            CompressedSAV.WithDecompressedAggregateSave(sourceSaves.Select(f => f.FilePath), stream =>
            {
                using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints, preserveValues))
                    result = GvasFile.FromFArchive(archiveReader, visitors);
            });
            return result;
        }
    }
}
