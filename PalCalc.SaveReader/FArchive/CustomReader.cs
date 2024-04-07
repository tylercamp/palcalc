using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSaveReader.FArchive
{
    public abstract class ICustomReader
    {
        public abstract string MatchedPath { get; }
        public abstract IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors);

        public static List<ICustomReader> All = new List<ICustomReader>()
        {
            new CharacterReader(),
            new CharacterContainerReader(),
        };
    }

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
    }

    public class CharacterContainerReader : ICustomReader
    {
        public override string MatchedPath => ".worldSaveData.CharacterContainerSaveData.Value.Slots.Slots.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
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

                return result;
            }
        }
    }

    public class CharacterDataPropertyMeta : BasicPropertyMeta
    {
        public Guid? GroupId => Id;
    }

    public class CharacterDataProperty : IProperty
    {
        public IPropertyMeta Meta { get; set; }

        public Dictionary<string, object> Data { get; set; }
    }

    public class CharacterReader : ICustomReader
    {
        public override string MatchedPath => ".worldSaveData.CharacterSaveParameterMap.Value.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path, visitors);

            using (var byteStream = new MemoryStream(arrayProp.ByteValues))
            using (var subReader = reader.Derived(byteStream))
            {
                var pathVisitors = visitors.Where(v => v.Matches(path));
                var extraVisitors = pathVisitors.SelectMany(v => v.VisitCharacterPropertyBegin(path)).ToList();

                var newVisitors = visitors.Concat(extraVisitors);

                var data = subReader.ReadPropertiesUntilEnd(path, visitors);
                subReader.ReadBytes(4); // unknown data?

                var meta = new CharacterDataPropertyMeta { Path = path, Id = subReader.ReadGuid() };

                foreach (var v in extraVisitors) v.Exit();
                foreach (var v in pathVisitors) v.VisitCharacterPropertyEnd(path, meta);

                return new CharacterDataProperty
                {
                    Meta = meta,
                    Data = data,
                };
            }
        }
    }
}
