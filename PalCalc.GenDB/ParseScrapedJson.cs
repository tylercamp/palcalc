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
            public class Parent
            {
                public string CodeName { get; set; }
                public string RequiredGender { get; set; }

                public PalGender? Gender => RequiredGender switch
                {
                    "Male" => PalGender.MALE,
                    "Female" => PalGender.FEMALE,
                    null => null,
                    _ => throw new NotImplementedException()
                };
            }

            public Parent Parent1 { get; set; }
            public Parent Parent2 { get; set; }
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

            public List<string> GuaranteedPassives { get; set; } = new List<string>();

            public JsonPalExclusiveBreeding ExclusiveBreeding { get; set; }
        }

        class JsonPassive
        {
            public string Name { get; set; }
            public string CodeName { get; set; }
            public bool IsPassive { get; set; }
            public int Rank { get; set; }
        }

        public static List<PassiveSkill> ReadPassives()
        {
            return JsonConvert
                .DeserializeObject<List<JsonPassive>>(File.ReadAllText("ref/scraped-passives.json"))
                .Select(jt => new PassiveSkill(jt.Name, jt.CodeName, jt.Rank))
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
                    GuaranteedPassivesInternalIds = jp.GuaranteedPassives,
                    Price = jp.Price,
                    MinWildLevel = jp.MinWildLevel,
                    MaxWildLevel = jp.MaxWildLevel,
                })
                .ToList();
        }

        // returns ((parent1, req-gender), (parent2, req-gender), child) (uses InternalName)
        public static List<((string, PalGender?), (string, PalGender?), string)> ReadExclusiveBreedings()
        {
            return JsonConvert
                .DeserializeObject<List<JsonPal>>(File.ReadAllText("ref/scraped-pals.json"))
                .Select(jp => jp.ExclusiveBreeding)
                .Where(eb => eb != null)
                .Select(eb =>
                    ((eb.Parent1.CodeName, eb.Parent1.Gender), (eb.Parent2.CodeName, eb.Parent2.Gender), eb.Child)
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
