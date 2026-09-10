using PalCalc.Model;
using PalCalc.Solver.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Probabilities
{
    public static class Passives
    {
        // note:
        //
        // this could all be precomputed at startup into a lookup table, but from my testing it's faster to redo this calculation each time
        // (likely memory bandwidth issue)

        // note: pascal's triangle is also 0-indexed! `0 choose 0` is a valid operation
        /// <summary>
        /// `n` Choose `k`
        /// </summary>
        public static int Choose(int n, int k) => k > n ? 0 : PascalsTriangle.Instance[n][k];

        /// <summary>
        /// Calculates the probability of a child pal with `numFinalPassives` passive skills having the all desired passives from
        /// the list of possible parent passives.
        /// </summary>
        /// 
        /// <param name="parentPassives">The the full set of passive skills from the parents (deduplicated)</param>
        /// <param name="desiredParentPassives">The list of passive skills you want to be inherited</param>
        /// <param name="numFinalPassives">The exact amount of final passive skills to calculate for</param>
        /// <returns></returns>
        /// 
        /// <remarks>
        /// "if we decide the child pal has N passive skills, what's the probability of containing all of the passives we want"
        /// </remarks>
        /// <remarks>
        /// Should be used repeatedly to calculate probabilities for all possible counts of passive skills (max 4)
        /// </remarks>
        /// 
        public static float ProbabilityInheritedTargetPassives(
            BreedingMechanics mechanics,
            List<PassiveSkill> parentPassives,
            List<PassiveSkill> desiredParentPassives,
            int numFinalPassives
        )
        {
            ArgumentNullException.ThrowIfNull(mechanics);

#if DEBUG && DEBUG_CHECKS
            if (parentPassives.Count != parentPassives.Distinct().Count()) Debugger.Break();
            if (desiredParentPassives.Count != desiredParentPassives.Distinct().Count()) Debugger.Break();
#endif

            // we know we need at least `desiredParentPassives.Count` to be inherited from the parents, but the overall number
            // of passives must be `numFinalPassives`. consider N, N+1, ..., passives inherited from parents, and an inverse amount
            // of randomly-added passives
            //
            // say we want 4 total passives with 2 desired from parents. we could have:
            //
            // - 2 inherited + 2 random
            // - 3 inherited + 1 random
            // - 4 inherited + 0 random
            //
            // ... each of these has a separate probability of getting exactly that outcome.
            //
            // The final probability is the sum of these possible outcomes.

            float probabilityForNumPassives = 0.0f;

            for (int numInheritedFromParent = desiredParentPassives.Count; numInheritedFromParent <= GameConstants.MaxTotalPassives; numInheritedFromParent++)
            {
                probabilityForNumPassives += ProbabilityForInheritedCountRoll(
                    mechanics,
                    parentPassives,
                    desiredParentPassives,
                    numFinalPassives,
                    numInheritedFromParent,
                    mechanics.PassiveProbabilityDirect[numInheritedFromParent]
                );
            }

            return probabilityForNumPassives;
        }

        /// <summary>
        /// Calculates the probability for one fixed inherited-count roll, as used by Special Cakes.
        /// The number actually inherited is capped by the number of distinct parent passives.
        /// </summary>
        public static float ProbabilityInheritedTargetPassivesForFixedInheritRoll(
            BreedingMechanics mechanics,
            List<PassiveSkill> parentPassives,
            List<PassiveSkill> desiredParentPassives,
            int numFinalPassives,
            int inheritedCountRoll
        )
        {
            ArgumentNullException.ThrowIfNull(mechanics);
            if (inheritedCountRoll < 0 || inheritedCountRoll > GameConstants.MaxTotalPassives)
                throw new ArgumentOutOfRangeException(nameof(inheritedCountRoll));

#if DEBUG && DEBUG_CHECKS
            if (parentPassives.Count != parentPassives.Distinct().Count()) Debugger.Break();
            if (desiredParentPassives.Count != desiredParentPassives.Distinct().Count()) Debugger.Break();
#endif

            return ProbabilityForInheritedCountRoll(
                mechanics,
                parentPassives,
                desiredParentPassives,
                numFinalPassives,
                inheritedCountRoll,
                1f
            );
        }

        /// <summary>
        /// Given a specific number of passives inherited from the parents, and the probability
        /// of rolling for that many direct passives from the parents, returns the effective chance of
        /// getting `numFinalPassives` where those passives contain all the desired ones.
        /// </summary>
        private static float ProbabilityForInheritedCountRoll(
            BreedingMechanics mechanics,
            List<PassiveSkill> parentPassives,
            List<PassiveSkill> desiredParentPassives,
            int numFinalPassives,
            int numInheritedFromParent,
            float inheritedCountProbability
        )
        {
            // we may inherit more passives from the parents than the parents actually have (e.g. inherit 4 passives from parents with
            // 2 total passives), in which case we'd still inherit just two
            //
            // this doesn't affect probabilities of getting `numInherited`, but it affects the number of random passives which must
            // be added to each `numFinalPassives` and the number of combinations of parent passives that we check
            var actualNumInheritedFromParent = Math.Min(numInheritedFromParent, parentPassives.Count);

            if (
                // e.g. If there are 3 desired passives, this fn should give the probability of all ways
                // to at least get those 3. But if we're also told to only inherit 2 from the parents,
                // there's not enough space for all 3
                actualNumInheritedFromParent < desiredParentPassives.Count ||
                // Similarly, if we're asked for ways to get 2 passives, but are told to inherit 3 from
                // the parents, there's also not enough space for all 3
                actualNumInheritedFromParent > numFinalPassives
            )
                return 0f;

            var numIrrelevantFromParent = actualNumInheritedFromParent - desiredParentPassives.Count;
            var numIrrelevantFromRandom = numFinalPassives - actualNumInheritedFromParent;
            float probabilityGotRequiredFromParent;

            if (desiredParentPassives.Count == 0)
            {
                // just the chance of getting this number of passives from parents
                probabilityGotRequiredFromParent = inheritedCountProbability;
            }
            else if (numIrrelevantFromParent == 0)
            {
                // chance of getting exactly the required passives
                probabilityGotRequiredFromParent = inheritedCountProbability / Choose(parentPassives.Count, desiredParentPassives.Count);
            }
            else
            {
                // chance of getting `numInherited` which include the desired passives
                //
                // https://math.stackexchange.com/a/3642093 - "method 1"
                //
                // (that link is for 1 random, just change "1" for any `k`. the detail of "random" in that link is also irrelevant,
                // still works even though we want `k` "specific" elements instead of "random" elements.)

                // (available passives except desired)
                // choose
                // (required num irrelevant)
                var numCombinationsWithIrrelevantPassive = (float)Choose(
                    parentPassives.Count - desiredParentPassives.Count,
                    numIrrelevantFromParent
                );

                // (all available passives)
                // choose
                // (actual num inherited from parent)
                var numCombinationsWithAnyPassives = (float)Choose(
                    parentPassives.Count,
                    actualNumInheritedFromParent
                );

                var probabilityCombinationWithDesiredPassives = numCombinationsWithIrrelevantPassive / numCombinationsWithAnyPassives;

                probabilityGotRequiredFromParent =
                    probabilityCombinationWithDesiredPassives *
                    inheritedCountProbability;
            }

#if DEBUG && DEBUG_CHECKS
            if (probabilityGotRequiredFromParent > 1) Debugger.Break();
#endif

            // we've inherited as many passives as we can get from the parents, now we need to fill in the remaining with
            // random passives until we get `numFinalPassives`

            // if we inherit 4 passives and "need exactly 0 random", then the random probability doesn't matter
            // at all - no additional random passives would be added. in that case we don't need exactly `N`
            // random passives, we just need _at least_ `N` random passives
            var probabilityGotExactRequiredRandom = numFinalPassives == GameConstants.MaxTotalPassives
                ? mechanics.PassiveRandomAddedAtLeastN[numIrrelevantFromRandom]
                : mechanics.PassiveRandomAddedProbability[numIrrelevantFromRandom];

            return probabilityGotRequiredFromParent * probabilityGotExactRequiredRandom;
        }
    }
}
