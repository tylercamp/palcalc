using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public static class SolverExtensions
    {
        public static int NumWildPalParticipants(this IPalReference pref)
        {
            switch (pref)
            {
                case BredPalReference bpr: return NumWildPalParticipants(bpr.Parent1) + NumWildPalParticipants(bpr.Parent2);
                case OwnedPalReference opr: return 0;
                case WildPalReference wpr: return 1;
                case CompositeOwnedPalReference c: return 0;
                default: throw new Exception($"Unhandled pal reference type {pref.GetType()}");
            }
        }

        public static List<Trait> ToDedicatedTraits(this IEnumerable<Trait> actualTraits, IEnumerable<Trait> desiredTraits)
        {
            var irrelevantAsRandom = actualTraits.Except(desiredTraits).Select(_ => new RandomTrait());
            return actualTraits.Intersect(desiredTraits).Concat(irrelevantAsRandom).ToList();
        }

        public static IEnumerable<IPalReference> AllReferences(this IPalReference pref)
        {
            yield return pref;

            switch (pref)
            {
                case BredPalReference bpr:
                    foreach (var r in bpr.Parent1.AllReferences()) yield return r;
                    foreach (var r in bpr.Parent2.AllReferences()) yield return r;
                    break;
            }
        }
    }
}
