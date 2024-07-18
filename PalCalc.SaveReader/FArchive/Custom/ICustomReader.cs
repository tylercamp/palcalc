using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    // ".worldSaveData.BaseCampSaveData"
    // ".worldSaveData.MapObjectSaveData"
    public abstract class ICustomReader
    {
        public abstract string MatchedPath { get; }
        public abstract IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors);

        public static List<ICustomReader> All = new List<ICustomReader>()
        {
            new CharacterReader(),
            new CharacterContainerReader(),
            new GroupReader(),
            new BaseCampReader(),
            new WorkerDirectorReader(),
        };
    }
}
