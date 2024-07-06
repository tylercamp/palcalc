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

            public List<string> GuaranteedTraits { get; set; } = new List<string>();
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
                    // TODO - bring in remaining properties to `Pal` type
                })
                .ToList();
        }
    }
}
