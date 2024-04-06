using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSaveReader.FArchive
{
    internal class FArchiveReader : IDisposable
    {
        BinaryReader reader;
        Dictionary<string, string> typeHints;
        string basePath;

        public FArchiveReader(Stream stream, Dictionary<string, string> typeHints, string basePath)
        {
            reader = new BinaryReader(stream);
            this.typeHints = typeHints;
            this.basePath = basePath;
        }

        public bool ReadBool() => reader.ReadBoolean();
        public Int16 ReadInt16() => reader.ReadInt16();
        public UInt16 ReadUInt16() => reader.ReadUInt16();
        public Int32 ReadInt32() => reader.ReadInt32();
        public UInt32 ReadUInt32() => reader.ReadUInt32();
        public Int64 ReadInt64() => reader.ReadInt64();
        public UInt64 ReadUInt64() => reader.ReadUInt64();
        public Single ReadFloat() => reader.ReadSingle();
        public Double ReadDouble() => reader.ReadDouble();
        public Byte ReadByte() => reader.ReadByte();

        public byte[] ReadBytes(int length) => reader.ReadBytes(length);

        public void Skip(int count) => reader.ReadBytes(count);

        public Guid ReadGuid() => new Guid(ReadBytes(16));

        public Guid? ReadOptionalGuid()
        {
            if (ReadBool()) return ReadGuid();
            else return null;
        }

        string GetTypeOr(string path, string fallback)
        {
            return typeHints.ContainsKey(path) ? typeHints[path] : fallback;
        }

        public FArchiveReader Derived(Stream data, string basePath)
        {
            return new FArchiveReader(data, typeHints, basePath);
        }

        public Dictionary<string, object> ReadPropertiesUntilEnd(string path = "")
        {
            if (path == "") path = basePath;

            var result = new Dictionary<string, object>();
            while (true)
            {
                var name = ReadString();
                if (name == "None") break;

                var typeName = ReadString();
                var size = ReadUInt64();
                var value = ReadProperty(typeName, size, $"{path}.{name}");

                result.Add(name, value);
            }
            return result;
        }

        private IProperty ReadStruct(string path)
        {
            var structType = ReadString();
            return new StructProperty
            {
                TypedMeta = new StructPropertyMeta
                {
                    Path = path,
                    StructTypeId = ReadGuid(),
                    Id = ReadOptionalGuid(),
                    StructType = structType,
                },
                Value = ReadStructValue(structType, path)
            };
        }

        private object ReadStructValue(string structType, string path = "")
        {
            switch (structType)
            {
                case "DateTime": return ReadUInt64();
                case "Guid": return ReadGuid();

                case "Vector":
                    return new VectorLiteral
                    {
                        x = ReadDouble(),
                        y = ReadDouble(),
                        z = ReadDouble(),
                    };

                case "Quat":
                    return new QuaternionLiteral
                    {
                        x = ReadDouble(),
                        y = ReadDouble(),
                        z = ReadDouble(),
                        w = ReadDouble(),
                    };

                case "LinearColor":
                    return new LinearColorLiteral
                    {
                        r = ReadFloat(),
                        g = ReadFloat(),
                        b = ReadFloat(),
                        a = ReadFloat(),
                    };

                default:
                    // treat as property list?
                    return ReadPropertiesUntilEnd(path);
            }
        }

        object ReadPropValue(string typeName, string structTypeName, string path)
        {
            switch (typeName)
            {
                case "StructProperty": return ReadStructValue(structTypeName, path);

                case "NameProperty":
                case "EnumProperty": return ReadString();

                case "IntProperty": return ReadInt32();
                case "BoolProperty": return ReadBool();
                default: throw new Exception("Unrecognized type name: " + typeName);
            }
        }

        public IProperty ReadProperty(string typeName, ulong size, string path, string nestedCallerPath = "")
        {
            // TODO - custom types
            var customReader = ICustomReader.All.SingleOrDefault(r => r.MatchedPath == path);
            if (customReader != null && (path != nestedCallerPath || nestedCallerPath == ""))
                return customReader.Decode(this, typeName, size, path);

            switch (typeName)
            {
                case "IntProperty": return LiteralProperty.Create(path, ReadOptionalGuid(), ReadInt32());
                case "Int64Property": return LiteralProperty.Create(path, ReadOptionalGuid(), ReadInt64());
                case "FixedPoint64Property": return LiteralProperty.Create(path, ReadOptionalGuid(), ReadInt32()); // ?????????????
                case "FloatProperty": return LiteralProperty.Create(path, ReadOptionalGuid(), ReadFloat());
                case "StrProperty": return LiteralProperty.Create(path, ReadOptionalGuid(), ReadString());
                case "NameProperty": return LiteralProperty.Create(path, ReadOptionalGuid(), ReadString());

                // init order is reversed?
                case "BoolProperty": return LiteralProperty.Create(path, ReadBool(), ReadOptionalGuid());

                case "EnumProperty":
                    return new EnumProperty
                    {
                        TypedMeta = new EnumPropertyMeta { Path = path, EnumType = ReadString(), Id = ReadOptionalGuid() },
                        EnumValue = ReadString(),
                    };

                case "StructProperty":
                    return ReadStruct(path);

                case "ArrayProperty":
                    {
                        var arrayType = ReadString();
                        var id = ReadOptionalGuid();

                        var count = ReadUInt32();
                        if (arrayType == "StructProperty")
                        {
                            var propertyName = ReadString();
                            var propertyType = ReadString();

                            ReadUInt64(); // ?

                            var arrayTypeName = ReadString();

                            var valueId = ReadGuid();

                            ReadBytes(1); // ?

                            var values = Enumerable.Range(0, (int)count).Select(_ => ReadStructValue(arrayTypeName, $"{path}.{propertyName}")).ToArray();

                            return new ArrayProperty
                            {
                                TypedMeta = new ArrayPropertyMeta
                                {
                                    Path = path,
                                    Id = valueId,
                                    PropName = propertyName,
                                    PropType = propertyType,
                                    TypeName = arrayTypeName,
                                    ContentId = valueId,
                                },
                                Value = values
                            };
                        }
                        else
                        {
                            object content;
                            var iteration = Enumerable.Range(0, (int)count);
                            switch (arrayType)
                            {
                                case "NameProperty":
                                case "EnumProperty":
                                    content = iteration.Select(_ => ReadString()).ToArray();
                                    break;

                                case "Guid":
                                    content = iteration.Select(_ => ReadGuid()).ToArray();
                                    break;

                                case "ByteProperty":
                                    if (count != size - 4) throw new Exception("Labelled ByteProperty not implemented"); // sic

                                    content = ReadBytes((int)count).ToArray();
                                    break;

                                default:
                                    throw new Exception("Unknown array type: " + arrayType + " at " + path);
                            }

                            return new ArrayProperty
                            {
                                TypedMeta = new ArrayPropertyMeta { Path = path, ArrayType = arrayType, Id = id },
                                Value = content
                            };
                        }
                    }

                case "MapProperty":
                    {
                        var keyType = ReadString();
                        var valueType = ReadString();
                        var valueId = ReadOptionalGuid();

                        ReadUInt32(); // ?

                        var count = ReadUInt32();

                        var keyPath = path + ".Key";
                        var valuePath = path + ".Value";

                        var keyStructType = keyType == "StructProperty" ? GetTypeOr(keyPath, "Guid") : null;
                        var valueStructType = valueType == "StructProperty" ? GetTypeOr(valuePath, "StructProperty") : null;

                        var values = Enumerable.Range(0, (int)count).Select(_ =>
                        {
                            var key = ReadPropValue(keyType, keyStructType, keyPath);
                            var value = ReadPropValue(valueType, valueStructType, valuePath);
                            return (key, value);
                        }).ToDictionary(p => p.key, p => p.value);

                        return new MapProperty
                        {
                            TypedMeta = new MapPropertyMeta
                            {
                                Path = path,

                                Id = valueId,
                                KeyType = keyType,
                                ValueType = valueType,
                                KeyStructType = keyStructType,
                                ValueStructType = valueStructType,
                            },

                            Value = values
                        };
                    }

                default: throw new Exception("Unrecognized type name: " + typeName);
            }
        }

        public string ReadString()
        {
            var size = ReadInt32();
            if (size == 0) return "";

            if (Math.Abs(size) > 100) Debugger.Break();

            Encoding encoding;
            byte[] bytes;

            if (size < 0)
            {
                size = -size * 2;
                bytes = ReadBytes(size);
                encoding = Encoding.Unicode; // utf-16-le

                size -= 2;
            }
            else
            {
                bytes = ReadBytes(size);
                encoding = Encoding.ASCII;

                size -= 1;
            }

            return encoding.GetString(bytes, 0, size);
        }

        public T[] ReadArray<T>(Func<FArchiveReader, T> reader)
        {
            var count = ReadUInt32();
            var result = new T[count];
            for (int i = 0; i < count; i++)
                result[i] = reader(this);
            return result;
        }

        public void Dispose()
        {
            reader.Dispose();
        }
    }
}
