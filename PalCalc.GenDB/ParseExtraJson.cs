using Newtonsoft.Json;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    internal static class ParseExtraJson
    {
        class JsonPal
        {
            public string Name { get; set; }
            public string CodeName { get; set; }
            public int PalDexNo { get; set; }
            public bool IsVariant { get; set; }
            public int RunSpeed { get; set; }
            public int RideSprintSpeed { get; set; }
            public int Stamina { get; set; }
            public int BreedPower { get; set; }
            public int IndexOrder { get; set; }
            public bool Mount { get; set; }
            public MountType MountType { get; set; }
            public int Rarity { get; set; }

            public List<string> GuaranteedTraits { get; set; } = new List<string>();
        }

        class JsonTrait
        {
            public string Name { get; set; }
            public string CodeName { get; set; }
            public bool IsPassive { get; set; }
            public int Rank { get; set; }
        }

        class JsonData
        {
            public List<JsonPal> Pals { get; set; }
            public List<JsonTrait> Traits { get; set; }
        }

        private static readonly string FilePath = "ref/extra.json";

        private static JsonData _json;
        private static JsonData Json => _json ??= JsonConvert.DeserializeObject<JsonData>(File.ReadAllText(FilePath));

        public static List<Trait> ReadTraits()
        {
            return Json.Traits.Select(t => new Trait(t.Name, t.CodeName, t.Rank)).ToList();
        }

        public static List<Pal> ReadPals()
        {
            return Json.Pals.Select(p => new Pal()
            {
                Name = p.Name,
                InternalName = p.CodeName,
                Id = new PalId() { PalDexNo = p.PalDexNo, IsVariant = p.IsVariant },
                RideWalkSpeed = p.RunSpeed,
                RideSprintSpeed = p.RideSprintSpeed,
                Stamina = p.Stamina,
                BreedingPower = p.BreedPower,
                InternalIndex = p.IndexOrder,
                CanMount = p.Mount,
                MountType = p.MountType,
                Rarity = p.Rarity,
                GuaranteedTraitInternalIds = p.GuaranteedTraits,
            }).ToList();
        }
    }
}
