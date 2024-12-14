using PalCalc.Model;
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
        /// <summary>
        /// `n` Choose `k`
        /// </summary>
        static int Choose(int n, int k) => PascalsTriangle.Instance[n - 1][k - 1];

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
        /// e.g. "if we decide the child pal has N passive skills, what's the probability of containing all of the passives we want"
        /// </remarks>
        /// <remarks>
        /// Should be used repeatedly to calculate probabilities for all possible counts of passive skills (max 4)
        /// </remarks>
        /// 
        public static float ProbabilityInheritedTargetPassives(List<PassiveSkill> parentPassives, List<PassiveSkill> desiredParentPassives, int numFinalPassives)
        {
            // we know we need at least `desiredParentPassives.Count` to be inherited from the parents, but the overall number
            // of passives must be `numFinalPassives`. consider N, N+1, ..., passives inherited from parents, and an inverse amount
            // of randomly-added passives
            //
            // e.g. we want 4 total passives with 2 desired from parents. we could have:
            //
            // - 2 inherited + 2 random
            // - 3 inherited + 1 random
            // - 4 inherited + 0 random
            //
            // ... each of these has a separate probability of getting exactly that outcome.
            //
            // the final probability for these params (fn args) is the sum

            float probabilityForNumPassives = 0.0f;

            for (int numInheritedFromParent = desiredParentPassives.Count; numInheritedFromParent <= numFinalPassives; numInheritedFromParent++)
            {
                // we may inherit more passives from the parents than the parents actually have (e.g. inherit 4 passives from parents with
                // 2 total passives), in which case we'd still inherit just two
                //
                // this doesn't affect probabilities of getting `numInherited`, but it affects the number of random passives which must
                // be added to each `numFinalPassives` and the number of combinations of parent passives that we check
                var actualNumInheritedFromParent = Math.Min(numInheritedFromParent, parentPassives.Count);

                var numIrrelevantFromParent = actualNumInheritedFromParent - desiredParentPassives.Count;
                var numIrrelevantFromRandom = numFinalPassives - actualNumInheritedFromParent;

#if DEBUG && DEBUG_CHECKS
                if (numIrrelevantFromRandom < 0) Debugger.Break();
#endif

                float probabilityGotRequiredFromParent;
                if (numInheritedFromParent == 0)
                {
                    // would only happen if neither parent has a desired passive

                    // the only way we could get zero inherited passives is if neither parent actually has any passives, otherwise
                    // it (seems to) be impossible to get zero direct inherited passives (unconfirmed from reddit thread)
                    if (parentPassives.Count > 0) continue;

                    // if neither parent has any passives, we'll always get 0 inherited passives, so we'll always get the "required"
                    // passives regardless of the roll for `PassiveProbabilityDirect`
                    probabilityGotRequiredFromParent = 1.0f;
                }
                else if (desiredParentPassives.Count == 0)
                {
                    // just the chance of getting this number of passives from parents
                    probabilityGotRequiredFromParent = GameConstants.PassiveProbabilityDirect[numInheritedFromParent];
                }
                else if (numIrrelevantFromParent == 0)
                {
                    // chance of getting exactly the required passives
                    probabilityGotRequiredFromParent = GameConstants.PassiveProbabilityDirect[numInheritedFromParent] / Choose(parentPassives.Count, desiredParentPassives.Count);
                }
                else
                {
                    // (available passives except desired)
                    // choose
                    // (required num irrelevant)
                    var numCombinationsWithIrrelevantPassive = (float)Choose(parentPassives.Count - desiredParentPassives.Count, numIrrelevantFromParent);

                    // (all available passives)
                    // choose
                    // (actual num inherited from parent)
                    var numCombinationsWithAnyPassives = (float)Choose(parentPassives.Count, actualNumInheritedFromParent);

                    // probability of those passives containing the desired passives
                    // (doesn't affect anything if we don't actually want any of these passives)
                    // (TODO - is this right? got this simple division from chatgpt)
                    var probabilityCombinationWithDesiredPassives = desiredParentPassives.Count == 0 ? 1 : (
                        numCombinationsWithIrrelevantPassive / numCombinationsWithAnyPassives
                    );

                    probabilityGotRequiredFromParent = probabilityCombinationWithDesiredPassives * GameConstants.PassiveProbabilityDirect[numInheritedFromParent];
                }

#if DEBUG && DEBUG_CHECKS
                if (probabilityGotRequiredFromParent > 1) Debugger.Break();
#endif

                var probabilityGotExactRequiredRandom = GameConstants.PassiveRandomAddedProbability[numIrrelevantFromRandom];
                probabilityForNumPassives += probabilityGotRequiredFromParent * probabilityGotExactRequiredRandom;
            }

            return probabilityForNumPassives;
        }
    }
}
