using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    public class BaseCampDataPropertyMeta : BasicPropertyMeta
    {
    }

    public class BaseCampDataProperty : ICustomProperty
    {
        public IPropertyMeta Meta { get; set; }

        public string Name { get; set; }
        public byte State { get; set; }
        public FullTransform Transform { get; set; }
        public float AreaRange { get; set; }
        public Guid GroupIdBelongTo { get; set; }
        public FullTransform FastTravelLocalTransform { get; set; }
        public Guid OwnerMapObjectInstanceId { get; set; }

        public void Traverse(Action<IProperty> action) {}
    }

    public class BaseCampReader : ICustomByteArrayReader
    {
        private static ILogger logger = Log.ForContext<BaseCampReader>();

        public override string MatchedPath => ".worldSaveData.BaseCampSaveData.Value.RawData";

        protected override IProperty Decode(FArchiveReader subReader, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");

            var meta = new BaseCampDataPropertyMeta()
            {
                Path = path,
                Id = subReader.ReadGuid(),
            };

            var result = new BaseCampDataProperty()
            {
                Meta = meta,
                Name = subReader.ReadString(),
                State = subReader.ReadByte(),
                Transform = subReader.ReadFullTransform(),
                AreaRange = subReader.ReadFloat(),
                GroupIdBelongTo = subReader.ReadGuid(),
                FastTravelLocalTransform = subReader.ReadFullTransform(),
                OwnerMapObjectInstanceId = subReader.ReadGuid()
            };

            foreach (var v in visitors.Where(v => v.Matches(path)))
                v.VisitBaseCampProperty(path, result);

            logger.Verbose("done");
            return result;
        }
    }
}
