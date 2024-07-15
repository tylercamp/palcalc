using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class PalExtensions
    {
        public static Pal ToPal(this string s, PalDB db) => db.Pals.Single(p => p.Name == s);
        public static Pal ToPal(this string s, IEnumerable<Pal> pals) => pals.Single(p => p.Name == s);
        public static Pal InternalToPal(this string s, PalDB db) => db.Pals.Single(p => p.InternalName.ToLower() == s.ToLower());

        // GetValueOrElse a hackfix for change in "Variant" classification after change in data scraping method
        public static Pal ToPal(this PalId id, PalDB db) => db.PalsById.GetValueFromAny(id, id.InvertedVariant);
        public static Pal ToPal(this PalId id, IEnumerable<Pal> pals) => pals.Single(p => p.Id == id);

        private static Trait RAND_REF = new RandomTrait();
        public static Trait ToTrait(this string s, PalDB db)
        {
            if (s == RAND_REF.Name) return new RandomTrait();
            else if (db.TraitsByName.ContainsKey(s)) return db.TraitsByName[s];
            else if (db.Traits.Any(t => t.InternalName == s)) return db.Traits.Single(t => t.InternalName == s);
            else return new UnrecognizedTrait(s);
        }
        public static Trait InternalToTrait(this string s, PalDB db)
        {
            if (s == RAND_REF.InternalName) return new RandomTrait();
            else return db.Traits.SingleOrDefault(t => t.InternalName == s) ?? new UnrecognizedTrait(s);
        }

        public static PalGender OppositeGender(this PalGender gender)
        {
            switch (gender)
            {
                case PalGender.MALE: return PalGender.FEMALE;
                case PalGender.FEMALE: return PalGender.MALE;
                case PalGender.WILDCARD: return PalGender.OPPOSITE_WILDCARD;
                case PalGender.OPPOSITE_WILDCARD: return PalGender.WILDCARD;
                default: throw new NotImplementedException();
            }
        }


        public static string TraitsListToString(this IEnumerable<Trait> traits)
        {
            if (!traits.Any()) return "no traits";

            var definite = traits.Where(t => t is not IUnknownTrait);
            var random = traits.Where(t => t is RandomTrait);
            var unrecognized = traits.Where(t => t is UnrecognizedTrait);

            var parts = new List<string>();
            if (definite.Any()) parts.Add(string.Join(", ", definite));
            if (random.Any()) parts.Add($"{random.Count()} random");
            if (unrecognized.Any()) parts.Add($"{unrecognized.Count()} unrecognized");

            return string.Join(", ", parts);
        }
    }
}
