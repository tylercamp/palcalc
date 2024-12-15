using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Probabilities
{
    // https://github.com/tylercamp/palcalc/issues/22#issuecomment-2509171056
    public static class IVs
    {
        // [NumDesired - 1]
        // eg if there's 1 specific type of IV we want, then ivDP[0] gives the probability of getting that IV
        // (not including 50/50 chance of inheriting from specific parent)
        private static float[] ivDesiredProbabilities;

        static IVs()
        {
            // for IV inheritance there's the probability of:
            //
            // 1. the chance of inheriting exactly N IVs
            // 2. the chance of those IVs being what we want

            // (1) is stored in GameConstants and can change depending on game updates
            // (2) is represented in `combinationsProbabilityTable` and is just from the possible combinations,
            //     regardless of game logic

            var combinationsProbabilityTable = new Dictionary<int, Dictionary<int, float>>()
            {
                // 1 inherited
                { 1, new() {
                    { 1, 1.0f / 3.0f }, // 1 desired
                    { 2, 0.0f },        // 2 desired (no way to get 2 if we only inherited 1)
                    { 3, 0.0f },        // 3 desired (...)
                } },

                // 2 inherited
                { 2, new() {
                    { 1, 2.0f / 3.0f }, // 1 desired
                    { 2, 1.0f / 3.0f }, // 2 desired
                    { 3, 0.0f }         // 3 desired (no way to get 3 if we only inherited 2
                } },

                // 3 inherited
                { 3, new() {
                    // 3 inherited means all IVs inherited, doesn't matter what IV we actually wanted, we'll always get it
                    { 1, 1.0f },
                    { 2, 1.0f },
                    { 3, 1.0f }
                } }
            };

            /*
            IV probabilities have similar approach as `Passives.ProbabilityInheritedTargetPassives` - a pal will end up inheriting
            exactly 1, 2, or 3 IVs; get the probability of each case combined with the probability of those cases giving us
            what we want
            */

            // stores the final probabilities of getting some number of desired IVs
            //
            // (the final real probability will also need to account for 50/50 chance of inheriting the IV from either specific parent)
            ivDesiredProbabilities = new float[3];
            for (int i = 0; i < 3; i++) ivDesiredProbabilities[i] = 0.0f;

            for (int numInherited = 1; numInherited <= 3; numInherited++)
            {
                var probabilityInherited = GameConstants.IVProbabilityDirect[numInherited];
                for (int numDesired = 1; numDesired <= 3; numDesired++)
                {
                    var probabilityMatched = combinationsProbabilityTable[numInherited][numDesired];

                    ivDesiredProbabilities[numDesired - 1] += probabilityInherited * probabilityMatched;
                }
            }
        }

        /// <summary>
        /// Given the IVs from two parents, returns the probability of inheriting all desired IVs from the parents.
        /// 
        /// A desired IV is determined by whether it's a "relevant" IV. (i.e. targetted during solving)
        /// </summary>
        public static float ProbabilityInheritedTargetIVs(IV_Set a, IV_Set b)
        {
            // (note: use of `.Count` and superficial array creation doesn't seem to be significant for perf)
            IV_IValue[] hps = [a.HP, b.HP];
            IV_IValue[] attacks = [a.Attack, b.Attack];
            IV_IValue[] defenses = [a.Defense, b.Defense];

            int numRelevantHP = hps.Count(iv => iv.IsRelevant);
            int numRelevantAttack = attacks.Count(iv => iv.IsRelevant);
            int numRelevantDefense = defenses.Count(iv => iv.IsRelevant);

            int numRequiredIVs = 0;
            if (numRelevantHP > 0) numRequiredIVs++;
            if (numRelevantAttack > 0) numRequiredIVs++;
            if (numRelevantDefense > 0) numRequiredIVs++;

            if (numRequiredIVs == 0) return 1.0f;

            // base probability is the chance of getting the IV categories we want
            float result = ivDesiredProbabilities[numRequiredIVs - 1];

            // even if we got the right IV categories, we might not get the right parents/values
            //
            // for each IV:
            // - if 0 relevant values, we weren't trying to inherit it, no effect
            // - if 1 relevant value, we need to inherit from the right parent, extra 50/50 chance
            // - if 2 relevant values, inheriting from either parent would suffice, no effect
            //
            // so if any IV has just one relevant parent, cut the final probability in half
            if (numRelevantHP == 1) result *= 0.5f;
            if (numRelevantAttack == 1) result *= 0.5f;
            if (numRelevantDefense == 1) result *= 0.5f;

#if DEBUG && DEBUG_CHECKS
            if (result <= 0.0001f) Debugger.Break();
#endif

            return result;
        }
    }
}
