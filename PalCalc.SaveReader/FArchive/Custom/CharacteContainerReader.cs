using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    public class CharacterContainerDataPropertyMeta : BasicPropertyMeta
    {
        public Guid? InstanceId => Id;
        public Guid PlayerId { get; set; }
    }

    public class CharacterContainerDataProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public CharacterContainerDataPropertyMeta TypedMeta { get; set; }

        public byte PermissionTribeId;

        public void Traverse(Action<IProperty> action) { }
    }

    public class CharacterContainerReader : ICustomByteArrayReader
    {
        private static ILogger logger = Log.ForContext<CharacterContainerReader>();

        public override string MatchedPath => ".worldSaveData.CharacterContainerSaveData.Value.Slots.Slots.RawData";

        protected override IProperty Decode(FArchiveReader subReader, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");

            var pathVisitors = visitors.Where(v => v.Matches(path));

            var meta = new CharacterContainerDataPropertyMeta
            {
                Path = path,
                PlayerId = subReader.ReadGuid(),
                Id = subReader.ReadGuid(),
            };

            foreach (var v in pathVisitors) v.VisitCharacterContainerPropertyBegin(path, meta);

            var result = new CharacterContainerDataProperty
            {
                TypedMeta = meta,
                PermissionTribeId = subReader.ReadByte()
            };

            foreach (var v in pathVisitors) v.VisitCharacterContainerPropertyEnd(path, meta);

            logger.Verbose("done");
            return result;
        }
    }
}
