using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public static List<PassiveSkill> ToDedicatedPassives(this IEnumerable<PassiveSkill> actualPassives, IEnumerable<PassiveSkill> desiredPassives)
        {
            var irrelevantAsRandom = actualPassives
                .Except(desiredPassives)
                .Where(p => !p.TrackedEffects.Any(e => e.InternalName == PassiveSkillEffect.BreedSpeed))
                .Select(_ => new RandomPassiveSkill());

            return actualPassives
                .Select(p =>
                    desiredPassives.Contains(p) || p.TrackedEffects.Any(e => e.InternalName == PassiveSkillEffect.BreedSpeed)
                        ? p
                        : new RandomPassiveSkill()
                )
                .ToList();
        }

        // TODO - atm only Philanthropist affects breeding time. if another is added, how do they interact if both are on a given pal?
        //public static float ToTimeFactor(this IEnumerable<PassiveSkill> passives) =>
        //    passives
        //        .SelectMany(p => p.TrackedEffects)
        //        .Where(p => p.InternalName == PassiveSkillEffect.BreedSpeed)
        //        // value of '100' will halve the breeding time. '100' also seems to be a sort
        //        // of "default" for a bunch of other passives, so idk if the value is actually
        //        // being used or if it's just checked as a flag. will treat as a flag for now
        //        .Select(p => 0.5f)
        //        .DefaultIfEmpty(1.0f)
        //        .Min();

        public static float ToTimeFactor(this List<PassiveSkill> passives)
        {
            var timeFactor = 1.0f;

            foreach (var p in passives)
            {
                for (int i = 0; i < p.TrackedEffects.Count; i++)
                {
                    var e = p.TrackedEffects[i];
                    if (e.InternalName == PassiveSkillEffect.BreedSpeed)
                        timeFactor = 0.5f;
                }
            }

            return timeFactor;
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
