using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PalElement(string englishName, string internalName)
    {
        public string Name { get; } = englishName;
        public Dictionary<string, string> LocalizedNames { get; set; }

        public string InternalName { get; protected set; } = internalName;

        public override string ToString() => Name;

        public override bool Equals(object obj) => (obj as PalElement)?.InternalName == InternalName;
        public override int GetHashCode() => InternalName.GetHashCode();
    }

    public class UnknownPalElement
    {
        private UnknownPalElement() { }

        public static readonly UnknownPalElement Instance = new();
    }
}
