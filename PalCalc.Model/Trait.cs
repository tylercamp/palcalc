using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class Trait
    {
        public Trait(string name, string internalName, int rank)
        {
            Name = name;
            InternalName = internalName;
            Rank = rank;
        }

        public string Name { get; }
        public Dictionary<string, string> LocalizedNames { get; set; }

        public string InternalName { get; }
        public int Rank { get; }

        public override string ToString() => Name;

        public override bool Equals(object obj) => (obj as Trait)?.InternalName == InternalName;
        public override int GetHashCode() => InternalName.GetHashCode();
    }

    public interface IUnknownTrait { }

    public class UnrecognizedTrait : Trait, IUnknownTrait
    {
        public UnrecognizedTrait(string internalName) : base($"'{internalName}' (unrecognized)", internalName, 0) { }
    }

    public class RandomTrait : Trait, IUnknownTrait
    {
        public RandomTrait() : base("(Random)", "__VIRT_RAND__", 0) { }
    }
}
