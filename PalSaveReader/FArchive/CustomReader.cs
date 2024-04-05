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
        public abstract object Decode(FArchiveReader reader, string typeName, ulong size, string path);

        public static List<ICustomReader> All = new List<ICustomReader>()
        {
            new CharacterContainerReader(),
            new CharacterReader()
        };
    }

    struct CharacterContainerData
    {
        public Guid PlayerId;
        public Guid InstanceId;
        public byte PermissionTribeId;
    }

    class CharacterContainerReader : ICustomReader
    {
        public override string MatchedPath => ".worldSaveData.CharacterContainerSaveData.Value.Slots.Slots.RawData";
        public override object Decode(FArchiveReader reader, string typeName, ulong size, string path)
        {
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path);
            
            using (var byteStream = new MemoryStream(arrayProp.Value as byte[]))
            using (var subReader = reader.Derived(byteStream))
            {
                return new CharacterContainerData
                {
                    PlayerId = subReader.ReadGuid(),
                    InstanceId = subReader.ReadGuid(),
                    PermissionTribeId = subReader.ReadByte()
                };
            }
        }
    }

    struct CharacterData
    {
        public Guid GroupId;
        public object Data;
    }

    class CharacterReader : ICustomReader
    {
        public override string MatchedPath => ".worldSaveData.CharacterSaveParameterMap.Value.RawData";
        public override object Decode(FArchiveReader reader, string typeName, ulong size, string path)
        {
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path);

            using (var byteStream = new MemoryStream(arrayProp.Value as byte[]))
            using (var subReader = reader.Derived(byteStream))
            {
                var data = subReader.ReadPropertiesUntilEnd();
                subReader.ReadBytes(4); // unknown data?

                return new CharacterData
                {
                    Data = data,
                    GroupId = subReader.ReadGuid()
                };
            }
        }
    }
}
