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

        public FArchiveReader(Stream stream, Dictionary<string, string> typeHints)
        {
            reader = new BinaryReader(stream);
            this.typeHints = typeHints;
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
            if (typeHints.ContainsKey(path))
            {
                return typeHints[path];
            }
            else
            {
                Console.WriteLine($"Struct type for {path} not found, assuming {fallback}");
                return fallback;
            }
        }

        public FArchiveReader Derived(Stream data)
        {
            return new FArchiveReader(data, typeHints);
        }

        public Dictionary<string, object> ReadPropertiesUntilEnd(string path, IEnumerable<IVisitor> visitors)
        {
            var result = new Dictionary<string, object>();
            while (true)
            {
                var name = ReadString();
                if (name == "None") break;

                var typeName = ReadString();
                var size = ReadUInt64();
                var value = ReadProperty(typeName, size, $"{path}.{name}", "", visitors);

                result.Add(name, value);
            }
            return result;
        }

        private IProperty ReadStruct(string path, IEnumerable<IVisitor> visitors)
        {
            var structType = ReadString();
            var meta = new StructPropertyMeta
            {
                Path = path,
                StructTypeId = ReadGuid(),
                Id = ReadOptionalGuid(),
                StructType = structType,
            };

            var pathVisitors = visitors.Where(v => v.Matches(path)).ToList();
            var extraVisitors = pathVisitors.SelectMany(v => v.VisitStructPropertyBegin(path, meta)).ToList();
            var newVisitors = visitors.Concat(extraVisitors);

            var value = ReadStructValue(structType, path, newVisitors);

            foreach (var v in extraVisitors) v.Exit();
            foreach (var v in pathVisitors) v.VisitStructPropertyEnd(path, meta);

            return new StructProperty
            {
                TypedMeta = meta,
                Value = value
            };
        }

        private object ReadStructValue(string structType, string path, IEnumerable<IVisitor> visitors)
        {
            switch (structType)
            {
                case "DateTime": return ReadUInt64();
                case "Guid":
                    {
                        var r = ReadGuid();
                        foreach (var v in visitors.Where(v => v.Matches(path))) v.VisitGuid(path, r);
                        return r;
                    }

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
                    return ReadPropertiesUntilEnd(path, visitors);
            }
        }

        object ReadPropValue(string typeName, string structTypeName, string path, IEnumerable<IVisitor> visitors)
        {
            var pathVisitors = visitors.Where(v => v.Matches(path));
            switch (typeName)
            {
                case "StructProperty": return ReadStructValue(structTypeName, path, visitors);

                case "NameProperty":
                case "EnumProperty":
                    {
                        var r = ReadString();
                        foreach (var v in pathVisitors) v.VisitString(path, r);
                        return r;
                    }

                case "IntProperty":
                    {
                        var r = ReadInt32();
                        foreach (var v in pathVisitors) v.VisitInt(path, r);
                        return r;
                    }

                case "BoolProperty":
                    {
                        var r = ReadBool();
                        foreach (var v in pathVisitors) v.VisitBool(path, r);
                        return r;
                    }

                default: throw new Exception("Unrecognized type name: " + typeName);
            }
        }

        public IProperty ReadProperty(string typeName, ulong size, string path, string nestedCallerPath, IEnumerable<IVisitor> visitors)
        {
            // TODO - custom types
            var customReader = ICustomReader.All.SingleOrDefault(r => r.MatchedPath == path);
            if (customReader != null && (path != nestedCallerPath || nestedCallerPath == ""))
                return customReader.Decode(this, typeName, size, path, visitors);

            var pathVisitors = visitors.Where(v => v.Matches(path)).ToList();

            switch (typeName)
            {
                case "IntProperty":
                    {
                        var res = LiteralProperty.Create(path, ReadOptionalGuid(), ReadInt32());
                        foreach (var v in pathVisitors)
                        {
                            v.VisitLiteralProperty(path, res);
                            v.VisitInt(path, (Int32)res.Value);
                        }
                        return res;
                    }

                case "Int64Property":
                    {
                        var res = LiteralProperty.Create(path, ReadOptionalGuid(), ReadInt64());
                        foreach (var v in pathVisitors)
                        {
                            v.VisitLiteralProperty(path, res);
                            v.VisitInt64(path, (Int64)res.Value);
                        }
                        return res;
                    }

                case "FixedPoint64Property":
                    {
                        var res = LiteralProperty.Create(path, ReadOptionalGuid(), ReadInt32()); // ?????????????
                        foreach (var v in pathVisitors)
                        {
                            v.VisitLiteralProperty(path, res);
                            v.VisitDouble(path, (Int32)res.Value);
                        }
                        return res;
                    }

                case "FloatProperty":
                    {
                        var res = LiteralProperty.Create(path, ReadOptionalGuid(), ReadFloat());
                        foreach (var v in pathVisitors)
                        {
                            v.VisitLiteralProperty(path, res);
                            v.VisitFloat(path, (float)res.Value);
                        }
                        return res;
                    }

                case "StrProperty":
                    {
                        var res = LiteralProperty.Create(path, ReadOptionalGuid(), ReadString());
                        foreach (var v in pathVisitors)
                        {
                            v.VisitLiteralProperty(path, res);
                            v.VisitString(path, (string)res.Value);
                        }
                        return res;
                    }
                
                case "NameProperty":
                    {
                        var res = LiteralProperty.Create(path, ReadOptionalGuid(), ReadString());
                        foreach (var v in pathVisitors)
                        {
                            v.VisitLiteralProperty(path, res);
                            v.VisitString(path, (string)res.Value);
                        }
                        return res;
                    }

                // init order is reversed?
                case "BoolProperty":
                    {
                        var res = LiteralProperty.Create(path, ReadBool(), ReadOptionalGuid());
                        foreach (var v in pathVisitors)
                        {
                            v.VisitLiteralProperty(path, res);
                            v.VisitBool(path, (Boolean)res.Value);
                        }
                        return res;
                    }

                case "EnumProperty":
                    {
                        var meta = new EnumPropertyMeta { Path = path, EnumType = ReadString(), Id = ReadOptionalGuid() };
                        var extraVisitors = pathVisitors.SelectMany(v => v.VisitEnumPropertyBegin(path, meta)).ToList();
                        var newVisitors = visitors.Concat(extraVisitors);

                        var result = new EnumProperty
                        {
                            TypedMeta = meta,
                            EnumValue = ReadString(),
                        };

                        foreach (var v in newVisitors.Where(v => v.Matches(path)))
                        {
                            v.VisitString(path, result.EnumValue);
                        }

                        foreach (var v in pathVisitors)
                            v.VisitEnumPropertyEnd(path, meta);

                        return result;
                    }

                case "StructProperty":
                    return ReadStruct(path, visitors);

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

                            var meta = new ArrayPropertyMeta
                            {
                                Path = path,
                                Id = valueId,
                                PropName = propertyName,
                                PropType = propertyType,
                                TypeName = arrayTypeName,
                                ContentId = valueId,
                            };

                            var extraVisitors = pathVisitors.SelectMany(v => v.VisitArrayPropertyBegin(path, meta)).ToList();
                            var newVisitors = visitors.Concat(extraVisitors);

                            var values = Enumerable.Range(0, (int)count).Select(i =>
                            {
                                var extraEntryVisitors = newVisitors.Where(v => v.Matches(path)).SelectMany(v => v.VisitArrayEntryBegin(path, i, meta)).ToList();
                                var newEntryVisitors = newVisitors.Concat(extraEntryVisitors);

                                var r = ReadStructValue(arrayTypeName, $"{path}.{propertyName}", newEntryVisitors);

                                foreach (var v in extraEntryVisitors) v.Exit();
                                foreach (var v in newVisitors.Where(v => v.Matches(path))) v.VisitArrayEntryEnd(path, i, meta);

                                return r;
                            }).ToArray();

                            foreach (var v in extraVisitors) v.Exit();
                            foreach (var v in pathVisitors) v.VisitArrayPropertyEnd(path, meta);

                            return new ArrayProperty
                            {
                                TypedMeta = meta,
                                Value = values
                            };
                        }
                        else
                        {
                            var meta = new ArrayPropertyMeta { Path = path, ArrayType = arrayType, Id = id };

                            var extraVisitors = pathVisitors.SelectMany(v => v.VisitArrayPropertyBegin(path, meta));
                            var newVisitors = pathVisitors.Concat(extraVisitors); // no new path subparts

                            object content;
                            var iteration = Enumerable.Range(0, (int)count);
                            switch (arrayType)
                            {
                                case "NameProperty":
                                case "EnumProperty":
                                    content = iteration.Select(i =>
                                    {
                                        var r = ReadString();
                                        foreach (var v in newVisitors)
                                        {
                                            v.VisitArrayEntryBegin(path, i, meta);
                                            v.VisitString(path, r);
                                            v.VisitArrayEntryEnd(path, i, meta);
                                        }
                                        return r;
                                        
                                    }).ToArray();
                                    break;

                                case "Guid":
                                    content = iteration.Select(i =>
                                    {
                                        var r = ReadGuid();
                                        foreach (var v in newVisitors)
                                        {
                                            v.VisitArrayEntryBegin(path, i, meta);
                                            v.VisitGuid(path, r);
                                            v.VisitArrayEntryEnd(path, i, meta);
                                        }
                                        return r;
                                    }).ToArray();
                                    break;

                                case "ByteProperty":
                                    if (count != size - 4) throw new Exception("Labelled ByteProperty not implemented"); // sic

                                    content = ReadBytes((int)count).ToArray();
                                    break;

                                default:
                                    throw new Exception("Unknown array type: " + arrayType + " at " + path);
                            }

                            foreach (var v in extraVisitors) v.Exit();
                            foreach (var v in pathVisitors) v.VisitArrayPropertyEnd(path, meta);

                            return new ArrayProperty
                            {
                                TypedMeta = meta,
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

                        var meta = new MapPropertyMeta
                        {
                            Path = path,

                            Id = valueId,
                            KeyType = keyType,
                            ValueType = valueType,
                            KeyStructType = keyStructType,
                            ValueStructType = valueStructType,
                        };

                        var extraVisitors = pathVisitors.SelectMany(v => v.VisitMapPropertyBegin(path, meta)).ToList();
                        var newVisitors = visitors.Concat(extraVisitors);

                        var values = Enumerable.Range(0, (int)count).Select(index =>
                        {
                            var extraEntryVisitors = newVisitors.Where(v => v.Matches(path)).SelectMany(v => v.VisitMapEntryBegin(path, index, meta)).ToList();
                            var newEntryVisitors = newVisitors.Concat(extraEntryVisitors);

                            var key = ReadPropValue(keyType, keyStructType, keyPath, newEntryVisitors);
                            var value = ReadPropValue(valueType, valueStructType, valuePath, newEntryVisitors);

                            foreach (var v in extraEntryVisitors) v.Exit();
                            foreach (var v in newVisitors.Where(v => v.Matches(path))) v.VisitMapEntryEnd(path, index, meta);

                            return (key, value);
                        }).ToDictionary(p => p.key, p => p.value);

                        var result = new MapProperty
                        {
                            TypedMeta = meta,

                            Value = values
                        };

                        foreach (var v in extraVisitors) v.Exit();
                        foreach (var v in pathVisitors)
                            v.VisitMapPropertyEnd(path, meta);

                        return result;
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
