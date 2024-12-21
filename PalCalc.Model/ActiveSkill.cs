using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class ActiveSkill(string name, string internalName, PalElement element)
    {
        public string Name { get; } = name;
        public Dictionary<string, string> LocalizedNames { get; set; }

        public PalElement Element { get; } = element;

        public string InternalName { get; } = internalName;

        public bool CanInherit { get; set; }

        public int Power { get; set; }
        public float CooldownSeconds { get; set; }

        public override string ToString() => Name;

        public override bool Equals(object obj) => (obj as ActiveSkill)?.InternalName == InternalName;
        public override int GetHashCode() => InternalName.GetHashCode();
    }

    public class UnrecognizedActiveSkill : ActiveSkill
    {
        public UnrecognizedActiveSkill(string internalName) : base($"Unknown ({internalName})", internalName, null) { }
    }
}
