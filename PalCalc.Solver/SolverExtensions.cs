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

        public static float ToTimeFactor(this List<PassiveSkill> passives)
        {
            var timeFactor = 1.0f;

            // TODO - atm only Philanthropist affects breeding time. if another is added, how do they interact if both are on a given pal?
            foreach (var p in passives)
            {
                foreach (var e in p.TrackedEffects)
                {
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

        public static IEnumerable<T> TakeUntilCancelled<T>(this IEnumerable<T> e, CancellationToken token)
        {
            foreach (var res in e)
            {
                if (token.IsCancellationRequested) yield break;
                yield return res;
            }
        }
    }
}
