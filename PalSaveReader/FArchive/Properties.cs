using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSaveReader.FArchive
{
    internal struct LiteralProperty
    {
        public Guid? Id;
        public object Value;
    }

    struct EnumProperty
    {
        public Guid? Id;
        public string EnumType;
        public string EnumValue;
    }

    struct StructProperty
    {
        public Guid? Id;

        public string StructType;
        public Guid StructTypeId;

        public object Value;
    }

    struct ArrayProperty
    {
        public Guid? Id;
        public string ArrayType;
        public object Value;
    }

    struct ArrayStructProperty
    {
        public Guid? Id;
        public string PropName;
        public string PropType;
        public string TypeName;

        public object[] Values;
    }

    struct MapProperty
    {
        public Guid? Id;
        public string KeyType;
        public string ValueType;
        public string KeyStructType;
        public string ValueStructType;

        public object Value;
    }
}
