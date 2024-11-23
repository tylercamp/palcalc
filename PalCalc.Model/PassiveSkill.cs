using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PassiveSkill
    {
        public PassiveSkill(string name, string internalName, int rank)
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

        public override bool Equals(object obj) => (obj as PassiveSkill)?.InternalName == InternalName;
        public override int GetHashCode() => InternalName.GetHashCode();
    }

    public interface IUnknownPassive { }

    public class UnrecognizedPassiveSkill : PassiveSkill, IUnknownPassive
    {
        public UnrecognizedPassiveSkill(string internalName) : base($"'{internalName}' (unrecognized)", internalName, 0) { }
    }

    public class RandomPassiveSkill : PassiveSkill, IUnknownPassive
    {
        public RandomPassiveSkill() : base("(Random)", "__VIRT_RAND__", 0) { }
    }
}
