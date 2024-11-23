using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace PalCalc.SaveReader.FArchive
{
    public interface IProperty
    {
        IPropertyMeta Meta { get; }

        /// <summary>
        /// Calls `action` on each object contained within this property. Has no effect
        /// for single-value properties.
        /// </summary>
        void Traverse(Action<IProperty> action);
    }

    public interface IPropertyMeta
    {
        string Path { get; set; }
        Guid? Id { get; set; }
    }

    public class BasicPropertyMeta : IPropertyMeta
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }
    }

    public class LiteralProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public BasicPropertyMeta TypedMeta { get; set; }

        public object Value { get; set; }

        public override string ToString() => Value.ToString();

        public static LiteralProperty Create(string path, Guid? id, object value)
        {
            return new LiteralProperty
            {
                TypedMeta = new BasicPropertyMeta { Path = path, Id = id },
                Value = value
            };
        }

        public static LiteralProperty Create(string path, object value, Guid? guid) =>
            Create(path, guid, value);

        public void Traverse(Action<IProperty> action) { }
    }

    public class BytePropertyMeta : IPropertyMeta
    {
        public string Path { get; set; }
        public Guid? Id { get; set; } = null;
    }

    public class ByteProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public BytePropertyMeta TypedMeta { get; set; }

        public string Text { get; set; }
        public ushort Num { get; set; }

        public void Traverse(Action<IProperty> action) { }
    }

    public class EnumPropertyMeta : BasicPropertyMeta
    {
        public string EnumType { get; set; }
    }

    public class EnumProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public EnumPropertyMeta TypedMeta { get; set; }

        public string EnumValue { get; set; }

        public override string ToString() => $"({TypedMeta.EnumType}){EnumValue}";

        public void Traverse(Action<IProperty> action) { }
    }

    public class StructPropertyMeta : BasicPropertyMeta
    {
        public string StructType { get; set; }
        public Guid StructTypeId { get; set; }
    }

    public class StructProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public StructPropertyMeta TypedMeta { get; set; }

        public object Value { get; set; }

        public override string ToString() => $"({TypedMeta.StructType}){Value}";

        public void Traverse(Action<IProperty> action)
        {
            TryTraverse(Value, TypedMeta.StructType, action);
        }

        // note: synced with cases in `FArchiveReader.ReadStructValue`
        private static List<string> ignoredStructTypes = ["DateTime", "Guid", "Vector", "Quat", "LinearColor"];
        internal static void TryTraverse(object maybeStructProperty, string structType, Action<IProperty> action)
        {
            if (ignoredStructTypes.Contains(structType)) return;

            if (maybeStructProperty is IProperty)
            {
                var prop = maybeStructProperty as IProperty;
                action(prop);
                prop.Traverse(action);
            }
            else
            {
                foreach (var subVal in (maybeStructProperty as Dictionary<string, object>).Values)
                {
                    if (subVal is IProperty)
                    {
                        var subProp = subVal as IProperty;
                        action(subProp);
                        subProp.Traverse(action);
                    }
                }
            }
        }
    }

    public class ArrayPropertyMeta : BasicPropertyMeta
    {
        public string ArrayType { get; set; }

        // nullable, for ArrayStruct properties
        public string PropName { get; set; }
        public string PropType { get; set; }
        public string TypeName { get; set; }
        public Guid? ContentId { get; set; }

        public bool IsArrayStruct => TypeName != null;
    }

    public class ArrayProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public ArrayPropertyMeta TypedMeta { get; set; }

        public object Value { get; set; }

        public T[] Values<T>() => Value as T[];

        public void Traverse(Action<IProperty> action)
        {
            if (TypedMeta.ArrayType == "StructProperty")
            {
                foreach (var val in Values<object>())
                {
                    StructProperty.TryTraverse(val, TypedMeta.TypeName, action);
                }
            }
        }

        public string[] StringValues
        {
            get
            {
                if (TypedMeta.ArrayType == "NameProperty" || TypedMeta.ArrayType == "EnumProperty")
                    return Value as string[];
                else
                    throw new Exception($"ArrayProperty of type {TypedMeta.ArrayType} cannot be an array of strings");
            }
        }

        public byte[] ByteValues
        {
            get
            {
                if (TypedMeta.ArrayType == "ByteProperty")
                    return Value as byte[];
                else
                    throw new Exception($"ArrayProperty of type {TypedMeta.ArrayType} cannot be an array of bytes");
            }
        }
    }

    public class MapPropertyMeta : BasicPropertyMeta
    {
        public string KeyType { get; set; }
        public string ValueType { get; set; }
        public string KeyStructType { get; set; }
        public string ValueStructType { get; set; }
    }

    public class MapProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public MapPropertyMeta TypedMeta { get; set; }

        public Dictionary<object, object> Value { get; set; }

        public void Traverse(Action<IProperty> action)
        {
            foreach (var kvp in Value)
            {
                if (TypedMeta.KeyType == "StructProperty")
                    StructProperty.TryTraverse(kvp.Key, TypedMeta.KeyStructType, action);

                if (TypedMeta.ValueType == "StructProperty")
                    StructProperty.TryTraverse(kvp.Value, TypedMeta.ValueStructType, action);
            }
        }
    }
}
