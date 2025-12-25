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
        // thanks chatgpt
        // Returns the list of combinations of elements in the given list, where combinations are order-independent
        internal static IEnumerable<List<T>> Combinations<T>(this List<T> elements, int maxSubListSize, LocalListPool<T> pool)
        {
            // Use indices-based iteration with pooled output lists
            var indices = new int[maxSubListSize];
            for (int size = 0; size <= Math.Min(maxSubListSize, elements.Count); size++)
            {
                if (size == 0)
                {
                    var result = pool?.BorrowRaw() ?? [];
                    yield return result;
                    continue;
                }

                // Initialize indices
                for (int i = 0; i < size; i++) indices[i] = i;

                while (true)
                {
                    var result = pool?.BorrowRaw() ?? [];
                    for (int i = 0; i < size; i++)
                        result.Add(elements[indices[i]]);
                    yield return result;

                    // Advance to next combination
                    int k = size - 1;
                    while (k >= 0 && indices[k] == elements.Count - size + k)
                        k--;
                    if (k < 0) break;
                    indices[k]++;
                    for (int j = k + 1; j < size; j++)
                        indices[j] = indices[j - 1] + 1;
                }
            }
        }

        // Returns a list of passives acting as a representation of `actualPassives` being dedicated to some set of desired passives;
        // i.e., everything in `desired` is preserved, everything else is just `Random`
        public static List<PassiveSkill> ToDedicatedPassives(this IEnumerable<PassiveSkill> actualPassives, IEnumerable<PassiveSkill> desiredPassives)
        {
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

                case SurgeryTablePalReference stpr:
                    foreach (var r in stpr.Input.AllReferences()) yield return r;
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
