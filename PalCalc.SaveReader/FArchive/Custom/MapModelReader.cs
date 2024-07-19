using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    public class MapModelDataPropertyMeta : BasicPropertyMeta
    {
    }

    public class MapModelDataProperty : IProperty
    {
        public IPropertyMeta Meta { get; set; }

        public Guid ConcreteModelInstanceId { get; set; }
        public Guid BaseCampIdBelongTo { get; set; }
        public Guid GroupIdBelongTo { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public FullTransform InitialTransformCache { get; set; }
        public Guid RepairWorkId { get; set; }
        public Guid OwnerSpawnerLevelObjectInstanceId { get; set; }
        public Guid OwnerInstanceId { get; set; }
        public Guid BuildPlayerUid { get; set; }
        public byte InteractRestrictType { get; set; }
        public Guid StageInstanceIdBelongTo { get;set; }
        public bool StageInstanceIdBelongToValid { get; set; }

        // note: palworld 0.33.0 seems to have changed the format and the store for this might be wrong (?)
        public long CreatedAt { get; set; }

        public void Traverse(Action<IProperty> action) { }
    }

    public class MapModelReader : ICustomByteArrayReader
    {
        private static ILogger logger = Log.ForContext<CharacterReader>();

        public override string MatchedPath => ".worldSaveData.MapObjectSaveData.MapObjectSaveData.Model.RawData";

        protected override IProperty Decode(FArchiveReader subReader, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");

            var meta = new MapModelDataPropertyMeta()
            {
                Path = path,
                Id = subReader.ReadGuid(),
            };

            var result = new MapModelDataProperty() { Meta = meta };

            result.ConcreteModelInstanceId = subReader.ReadGuid();
            result.BaseCampIdBelongTo = subReader.ReadGuid();
            result.GroupIdBelongTo = subReader.ReadGuid();
            result.CurrentHp = subReader.ReadInt32();
            result.MaxHp = subReader.ReadInt32();
            result.InitialTransformCache = subReader.ReadFullTransform();
            result.RepairWorkId = subReader.ReadGuid();
            result.OwnerSpawnerLevelObjectInstanceId = subReader.ReadGuid();
            result.OwnerInstanceId = subReader.ReadGuid();
            result.BuildPlayerUid = subReader.ReadGuid();
            result.InteractRestrictType = subReader.ReadByte();
            result.StageInstanceIdBelongTo = subReader.ReadGuid();
            result.StageInstanceIdBelongToValid = subReader.ReadUInt32() > 0;
            result.CreatedAt = subReader.ReadInt64();

            foreach (var v in visitors.Where(v => v.Matches(path)))
                v.VisitMapModelProperty(path, result);

            logger.Verbose("done");

            return result;
        }
    }
}
