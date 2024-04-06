using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSaveReader.FArchive
{
    interface IProperty
    {
        string Path { get; set; }
        Guid? Id { get; set; }
    }

    internal class LiteralProperty : IProperty
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }

        public object Value;

        public override string ToString() => Value.ToString();
    }

    class EnumProperty : IProperty
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }

        public string EnumType { get; set; }
        public string EnumValue { get; set; }

        public override string ToString() => $"({EnumType}){EnumValue}";
    }

    class StructProperty : IProperty
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }

        public string StructType { get; set; }
        public Guid StructTypeId { get; set; }

        public object Value { get; set; }

        public override string ToString() => $"({StructType}){Value}";
    }

    class ArrayProperty : IProperty
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }

        public string ArrayType { get; set; }
        public object Value { get; set; }
    }

    class ArrayStructProperty : IProperty
    {
        public string Path { get; set; }

        public Guid? Id { get; set; }
        public string PropName { get; set; }
        public string PropType { get; set; }
        public string TypeName { get; set; }

        public object[] Values { get; set; }
    }

    class MapProperty : IProperty
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }
        public string KeyType { get; set; }
        public string ValueType { get; set; }
        public string KeyStructType { get; set; }
        public string ValueStructType { get; set; }

        public Dictionary<object, object> Value { get; set; }
    }
}
