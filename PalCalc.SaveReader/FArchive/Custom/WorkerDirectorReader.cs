using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    public class WorkerDirectorDataPropertyMeta : BasicPropertyMeta
    {
    }

    public class WorkerDirectorDataProperty : IProperty
    {
        public IPropertyMeta Meta { get; set; }

        public FullTransform SpawnTransform { get; set; }
        public byte CurrentOrderType { get; set; }
        public byte CurrentBattleType { get; set; }
        public Guid ContainerId { get; set; }

        public void Traverse(Action<IProperty> action) {}
    }

    public class WorkerDirectorReader : ICustomByteArrayReader
    {
        private static ILogger logger = Log.ForContext<WorkerDirectorReader>();

        public override string MatchedPath => ".worldSaveData.BaseCampSaveData.Value.WorkerDirector.RawData";

        protected override IProperty Decode(FArchiveReader subReader, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");

            var meta = new WorkerDirectorDataPropertyMeta()
            {
                Path = path,
                Id = subReader.ReadGuid(),
            };

            var result = new WorkerDirectorDataProperty()
            {
                Meta = meta,
                SpawnTransform = subReader.ReadFullTransform(),
                CurrentOrderType = subReader.ReadByte(),
                CurrentBattleType = subReader.ReadByte(),
                ContainerId = subReader.ReadGuid(),
            };

            foreach (var v in visitors.Where(v => v.Matches(path)))
                v.VisitWorkerDirectorProperty(path, result);

            logger.Verbose("done");
            return result;
        }
    }
}
