using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSaveReader.FArchive
{
    internal abstract class ICustomReader
    {
        public abstract string MatchedPath { get; }
        public abstract IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path);

        public static List<ICustomReader> All = new List<ICustomReader>()
        {
            new CharacterContainerReader(),
            new CharacterReader()
        };
    }

    class CharacterContainerDataProperty : IProperty
    {
        public string Path { get; set; }

        public Guid? Id { get; set; }

        public Guid? InstanceId => Id;

        public Guid PlayerId;
        public byte PermissionTribeId;
    }

    class CharacterContainerReader : ICustomReader
    {
        public override string MatchedPath => ".worldSaveData.CharacterContainerSaveData.Value.Slots.Slots.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path)
        {
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path);
            
            using (var byteStream = new MemoryStream(arrayProp.Value as byte[]))
            using (var subReader = reader.Derived(byteStream, path))
            {
                return new CharacterContainerDataProperty
                {
                    Path = path,
                    PlayerId = subReader.ReadGuid(),
                    Id = subReader.ReadGuid(),
                    PermissionTribeId = subReader.ReadByte()
                };
            }
        }
    }

    class CharacterDataProperty : IProperty
    {
        public string Path { get; set; }

        public Guid? Id { get; set; }
        public Guid? GroupId => Id;

        public object Data;
    }

    class CharacterReader : ICustomReader
    {
        public override string MatchedPath => ".worldSaveData.CharacterSaveParameterMap.Value.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path)
        {
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path);

            using (var byteStream = new MemoryStream(arrayProp.Value as byte[]))
            using (var subReader = reader.Derived(byteStream, path))
            {
                var data = subReader.ReadPropertiesUntilEnd();
                subReader.ReadBytes(4); // unknown data?

                return new CharacterDataProperty
                {
                    Path = path,
                    Data = data,
                    Id = subReader.ReadGuid()
                };
            }
        }
    }
}
