using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    // TODO - not used yet, was previously a basic field in the pal data, now I'm not sure how this is
    //        meant to be scraped
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MountType
    {
        None,
        Ground,
        Swim,
        Fly,
        FlyLand,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PalSize
    {
        None,
        XS,
        S,
        M,
        L,
        XL,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum WorkType
    {
        Kindling,
        Watering,
        Planting,
        GenerateElectricity,
        Handiwork,
        Gathering,
        Lumbering,
        Mining,
        // OilExtraction, // in the data files but not actually used?
        MedicineProduction,
        Cooling,
        Transporting,
        Farming,
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

        public PalSize Size { get; set; } = PalSize.None;

        public bool Nocturnal { get; set; } = false;

        public int CraftSpeed { get; set; } = 0;

        public int Hp { get; set; } = 0;
        public int Defense { get; set; } = 0;
        //public int Support { get; set; } = 0; // ?
        public int Attack { get; set; } = 0;

        public int WalkSpeed { get; set; } = 0;
        public int RunSpeed { get; set; } = 0;
        public int RideSprintSpeed { get; set; } = 0;
        public int TransportSpeed { get; set; } = 0;
        public int MaxFullStomach { get; set; } = 0;
        public int FoodAmount { get; set; } = 0;
        public int Stamina { get; set; } = 0;

        public Dictionary<WorkType, int> WorkSuitability { get; set; } = null;



        ////

        public override string ToString() => $"{Name} ({Id})";

        public static bool operator ==(Pal a, Pal b) => (ReferenceEquals(a, null) && ReferenceEquals(b, null)) || (a?.Equals(b) ?? false);
        public static bool operator !=(Pal a, Pal b) => !(a == b);

        public override bool Equals(object obj) => (obj as Pal)?.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
}
