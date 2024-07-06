using Newtonsoft.Json;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    internal static class ParseScrapedJson
    {
        class JsonPalExclusiveBreeding
        {
            public string Parent1 { get; set; }
            public string Parent2 { get; set; }
            public string Child { get; set; }
        }

        class JsonPal
        {
            public string Name { get; set; }
            public string CodeName { get; set; }
            public int PalDexNo { get; set; }
            public bool IsVariant { get; set; }
            public int BreedPower { get; set; }
            public int IndexOrder { get; set; }
            public int Price { get; set; }
            public int MaleProbability { get; set; }

            public int? MinWildLevel { get; set; }
            public int? MaxWildLevel { get; set; }

            public List<string> GuaranteedTraits { get; set; } = new List<string>();

            public JsonPalExclusiveBreeding ExclusiveBreeding { get; set; }
        }

        class JsonTrait
        {
            public string Name { get; set; }
            public string CodeName { get; set; }
            public bool IsPassive { get; set; }
            public int Rank { get; set; }
        }

        public static List<Trait> ReadTraits()
        {
            return JsonConvert
                .DeserializeObject<List<JsonTrait>>(File.ReadAllText("ref/scraped-traits.json"))
                .Select(jt => new Trait(jt.Name, jt.CodeName, jt.Rank))
                .ToList();
        }

        public static List<Pal> ReadPals()
        {
            return JsonConvert
                .DeserializeObject<List<JsonPal>>(File.ReadAllText("ref/scraped-pals.json"))
                .Select(jp => new Pal()
                {
                    Name = jp.Name,
                    InternalName = jp.CodeName,
                    Id = new PalId() { PalDexNo = jp.PalDexNo, IsVariant = jp.IsVariant },
                    BreedingPower = jp.BreedPower,
                    InternalIndex = jp.IndexOrder,
                    GuaranteedTraitInternalIds = jp.GuaranteedTraits,
                    Price = jp.Price,
                    MinWildLevel = jp.MinWildLevel,
                    MaxWildLevel = jp.MaxWildLevel,
                })
                .ToList();
        }

        // returns (parent1, parent2, child) (uses InternalName)
        public static List<(string, string, string)> ReadExclusiveBreedings()
        {
            return JsonConvert
                .DeserializeObject<List<JsonPal>>(File.ReadAllText("ref/scraped-pals.json"))
                .Select(jp => jp.ExclusiveBreeding)
                .Where(eb => eb != null)
                .Select(eb =>
                    (eb.Parent1, eb.Parent2, eb.Child)
                )
                .ToList();
        }

        public static Dictionary<string, Dictionary<PalGender, float>> ReadGenderProbabilities()
        {
            return JsonConvert
                .DeserializeObject<List<JsonPal>>(File.ReadAllText("ref/scraped-pals.json"))
                .ToDictionary(
                    jp => jp.CodeName,
                    jp => new Dictionary<PalGender, float>()
                    {
                        { PalGender.MALE, jp.MaleProbability / 100.0f },
                        { PalGender.FEMALE, 1 - (jp.MaleProbability / 100.0f) }
                    }
                );
        }
    }
}
