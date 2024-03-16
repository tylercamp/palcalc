using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.model
{
    internal class PalId
    {
        public int Id { get; set; }
        public bool IsVariant { get; set; }

        public static bool operator==(PalId a, PalId b)
        {
            if (ReferenceEquals(a, null) != ReferenceEquals(b, null)) return false;
            return a?.Equals(b) ?? true;
        }

        public static bool operator!=(PalId a, PalId b)
        {
            return !(a == b);
        }

        public override int GetHashCode() => IsVariant ? -Id : Id;

        public override bool Equals(object obj) => (obj as PalId)?.GetHashCode() == GetHashCode();

        public override string ToString() => Id.ToString() + (IsVariant ? ".1" : "");
    }
}
