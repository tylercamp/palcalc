using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.model
{
    [JsonConverter(typeof(StringEnumConverter))]
    enum PalGender
    {
        // note: pal world is not P.C., so it only has two genders
        MALE = 0b001,
        FEMALE = 0b010,
        // (but this program is P.C., and has four genders)
        WILDCARD = 0b011,
        OPPOSITE_WILDCARD = 0b100, // contextual - pals of this gender should be paired with pals of WILDCARD gender
    }

    internal class PalInstance
    {
        public Pal Pal { get; set; }
        public PalLocation Location { get; set; }
        public PalGender Gender { get; set; }
        public List<Trait> Traits { get; set; }

        public override string ToString() => $"{Gender} {Pal} at {Location} with traits ({string.Join(", ", Traits)})";

        public Serialized AsSerialized => new Serialized { PalId = Pal.Id, Location = Location, Gender = Gender, TraitInternalNames = Traits.Select(t => t.InternalName).ToList() };

        public class Serialized
        {
            public PalId PalId { get; set; }
            public PalLocation Location { get; set; }
            public PalGender Gender { get; set; }
            public List<string> TraitInternalNames { get; set; }
        }

        public static PalInstance FromSerialized(PalDB db, Serialized s) => new PalInstance
        {
            Pal = db.PalsById[s.PalId],
            Location = s.Location,
            Gender = s.Gender,
            Traits = s.TraitInternalNames.Select(n => db.Traits.Single(t => t.InternalName == n)).ToList()
        };

        public static string ListToJson(List<PalInstance> instances) => JsonConvert.SerializeObject(instances.Select(i => i.AsSerialized));
        public static List<PalInstance> JsonToList(PalDB db, string json) => JsonConvert.DeserializeObject<List<Serialized>>(json).Select(s => FromSerialized(db, s)).ToList();

    }
}
