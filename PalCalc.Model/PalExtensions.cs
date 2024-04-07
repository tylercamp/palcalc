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
        public static Pal ToPal(this PalId id, PalDB db) => db.PalsById[id];

        public static Trait ToTrait(this string s, PalDB db) => db.TraitsByName[s];

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

        public static bool EqualsTraits(this List<Trait> thisTraits, List<Trait> otherTraits)
        {
            // assume each list of traits is distinct, i.e. no repeated traits (except random)

            if (thisTraits.Count != otherTraits.Count) return false;

            var thisNonRandom = thisTraits.Where(t => t is not RandomTrait);
            var otherNonRandom = otherTraits.Where(t => t is not RandomTrait);

            var thisRandomCount = thisTraits.Count(t => t is RandomTrait);
            var otherRandomCount = otherTraits.Count(t => t is RandomTrait);

            return thisRandomCount == otherRandomCount && !thisNonRandom.Except(otherNonRandom).Any();
        }

        public static string TraitsListToString(this IEnumerable<Trait> traits)
        {
            if (!traits.Any()) return "no traits";

            var nonRandom = traits.Where(t => t is not RandomTrait);
            var randomCount = traits.Count(t => t is RandomTrait);

            var nonRandomString = string.Join(", ", nonRandom);
            if (randomCount == 0) return nonRandomString;
            
            return $"{nonRandomString}, {randomCount} random";
        }
    }
}
