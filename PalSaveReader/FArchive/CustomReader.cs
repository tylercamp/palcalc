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
            new CharacterReader()
        };
    }

    class CharacterDataPropertyMeta : BasicPropertyMeta
    {
        public Guid? GroupId => Id;
    }

    class CharacterDataProperty : IProperty
    {
        public IPropertyMeta Meta { get; set; }

        public object Data { get; set; }
    }

    class CharacterReader : ICustomReader
    {
        public override string MatchedPath => ".worldSaveData.CharacterSaveParameterMap.Value.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path)
        {
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path);

            using (var byteStream = new MemoryStream(arrayProp.ByteValues))
            using (var subReader = reader.Derived(byteStream, path))
            {
                var data = subReader.ReadPropertiesUntilEnd();
                subReader.ReadBytes(4); // unknown data?

                return new CharacterDataProperty
                {
                    Meta = new CharacterDataPropertyMeta { Path = path, Id = subReader.ReadGuid() },
                    Data = data,
                };
            }
        }
    }
}
