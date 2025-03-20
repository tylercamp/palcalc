using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MountType
    {
        None,
        Ground,
        Swim,
        Fly,
        FlyLand,
    }

    public class Pal
    {
        public PalId Id { get; set; }
        public string Name { get; set; }

        public Dictionary<string, string> LocalizedNames { get; set; }

        public string InternalName { get; set; }
        public int InternalIndex { get; set; } // index in the game's internal pal ordering (not paldex)

        public int BreedingPower { get; set; }

        //// fields introduced after first release will need a default value

        public int Price { get; set; } = 0;
        public int? MinWildLevel { get; set; } = null;
        public int? MaxWildLevel { get; set; } = null;
        
        public List<string> GuaranteedPassivesInternalIds { get; set; } = new List<string>();
        public IEnumerable<PassiveSkill> GuaranteedPassiveSkills(PalDB db) => GuaranteedPassivesInternalIds.Select(id => id.InternalToStandardPassive(db));

        public PartnerSkill PartnerSkill { get; set; } = null;

        public int Rarity { get; set; } = 0;

        // (palRank is 1-5)
        public IEnumerable<PassiveSkill> PartnerSkillPassives(PalDB db, int palRank) =>
            PartnerSkill
                ?.RankEffects
                ?.ElementAt(palRank - 1)
                ?.PassiveSkills(db);

        private EggSize? eggSize;
        [JsonIgnore]
        public EggSize EggSize => eggSize ??= GameConstants.EggSizeMinRarity.OrderBy(kvp => kvp.Value).First(kvp => kvp.Value <= Rarity).Key;

        ////

        public override string ToString() => $"{Name} ({Id})";

        public static bool operator ==(Pal a, Pal b) => (ReferenceEquals(a, null) && ReferenceEquals(b, null)) || (a?.Equals(b) ?? false);
        public static bool operator !=(Pal a, Pal b) => !(a == b);

        public override bool Equals(object obj) => (obj as Pal)?.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
}
