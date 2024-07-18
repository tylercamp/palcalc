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

    public class CharacterContainerReader : ICustomReader
    {
        private static ILogger logger = Log.ForContext<CharacterContainerReader>();

        public override string MatchedPath => ".worldSaveData.CharacterContainerSaveData.Value.Slots.Slots.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path, visitors);

            var pathVisitors = visitors.Where(v => v.Matches(path));

            using (var byteStream = new MemoryStream(arrayProp.Value as byte[]))
            using (var subReader = reader.Derived(byteStream))
            {
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
}
