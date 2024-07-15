using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PalId : IComparable<PalId>
    {
        public int PalDexNo { get; set; }
        public bool IsVariant { get; set; }

        [JsonIgnore]
        public PalId InvertedVariant => new PalId() { PalDexNo = PalDexNo, IsVariant = !IsVariant };

        public static bool operator ==(PalId a, PalId b)
        {
            if (ReferenceEquals(a, null) != ReferenceEquals(b, null)) return false;
            return a?.Equals(b) ?? true;
        }

        public static bool operator !=(PalId a, PalId b)
        {
            return !(a == b);
        }

        public override int GetHashCode() => IsVariant ? -PalDexNo : PalDexNo;

        public override bool Equals(object obj) => (obj as PalId)?.GetHashCode() == GetHashCode();

        public override string ToString() => PalDexNo.ToString() + (IsVariant ? ".1" : "");

        public int CompareTo(PalId other)
        {
            if (PalDexNo < other.PalDexNo) return -1;
            else if (PalDexNo > other.PalDexNo) return 1;
            else if (!IsVariant && other.IsVariant) return -1;
            else if (IsVariant && !other.IsVariant) return 1;
            else return 0;
        }
    }
}
