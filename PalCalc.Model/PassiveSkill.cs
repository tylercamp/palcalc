using Newtonsoft.Json;
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

        [JsonConverter(typeof(CaseInsensitiveStringDictionaryConverter<string>))]
        public Dictionary<string, string> LocalizedNames { get; set; }

        public string InternalName { get; }
        public int Rank { get; }

        /// <summary>
        /// Monetary cost to apply this passive via Surgery (0 if not applicable).
        /// </summary>
        public int SurgeryCost { get; set; } = 0;
        public string SurgeryRequiredItem { get; set; } = null;

        [JsonIgnore]
        public bool SupportsSurgery => SurgeryCost > 0;

        public string Description { get; set; }

        [JsonConverter(typeof(CaseInsensitiveStringDictionaryConverter<string>))]
        public Dictionary<string, string> LocalizedDescriptions { get; set; }

        // whether this is a passive you would find on a pal's list of passives,
        // rather than e.g. passive attached to a partner skill
        public bool IsStandardPassiveSkill { get; set; } = true;

        // whether this passive can be chosen when a pal gets a random skill
        public bool RandomInheritanceAllowed { get; set; } = false;

        public int RandomInheritanceWeight { get; set; } = 0;

        public List<PassiveSkillEffect> TrackedEffects { get; set; } = [];

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

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        private static ulong randomHash = 0;
        private int hash = (int)(Interlocked.Increment(ref randomHash) % int.MaxValue);
        public override int GetHashCode() => hash;
    }
}
