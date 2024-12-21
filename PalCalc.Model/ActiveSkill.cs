using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class ActiveSkill
    {
        public ActiveSkill(string name, string internalName, PalElement element)
        {
            Name = name;
            InternalName = internalName;
            Element = element;
            ElementInternalName = element?.InternalName;
        }

        public string Name { get; private set; }
        public Dictionary<string, string> LocalizedNames { get; set; }

        // (avoid serializing a whole copy of each element for a skill, the proper element objects will be resolved
        // by `PalDBSerializer`)

        [JsonProperty]
        internal string ElementInternalName { get; private set; }
        [JsonIgnore]
        public PalElement Element { get; internal set; }

        public string InternalName { get; private set; }

        public bool CanInherit { get; set; }

        public int Power { get; set; }
        public float CooldownSeconds { get; set; }

        public override string ToString() => Name;

        public override bool Equals(object obj) => (obj as ActiveSkill)?.InternalName == InternalName;
        public override int GetHashCode() => InternalName.GetHashCode();
    }

    public class UnrecognizedActiveSkill : ActiveSkill
    {
        public UnrecognizedActiveSkill(string internalName) : base($"'{internalName}' (unrecognized)", internalName, null) { }
    }
}
