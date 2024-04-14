using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class Trait
    {
        public Trait(string name, string internalName)
        {
            Name = name;
            InternalName = internalName;
        }

        public string Name { get; }
        public string InternalName { get; }

        public override string ToString() => Name;

        public override bool Equals(object obj) => (obj as Trait)?.InternalName == InternalName;
        public override int GetHashCode() => InternalName.GetHashCode();
    }

    public class RandomTrait : Trait
    {
        public RandomTrait() : base("(Random)", "__VIRT_RAND__") { }
    }
}
