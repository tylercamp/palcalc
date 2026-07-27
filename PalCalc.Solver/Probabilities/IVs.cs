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
        // The probability of ending up with N desired IVs (indexed [numDesired - 1]) is computed
        // centrally in GameConstants.IVDesiredProbabilities, from the scraped inherited-count
        // distribution. (Does not include the 50/50 chance of inheriting from a specific parent.)

        /// <summary>
        /// Given the IVs from two parents, returns the probability of inheriting all desired IVs from the parents.
        /// 
        /// A desired IV is determined by whether it's a "relevant" IV. (i.e. targetted during solving)
        /// </summary>
        public static float ProbabilityInheritedTargetIVs(IV_Set a, IV_Set b)
        {
            int numRelevantHP = 0;
            if (a.HP.IsRelevant) numRelevantHP++;
            if (b.HP.IsRelevant) numRelevantHP++;

            int numRelevantAttack = 0;
            if (a.Attack.IsRelevant) numRelevantAttack++;
            if (b.Attack.IsRelevant) numRelevantAttack++;

            int numRelevantDefense = 0;
            if (a.Defense.IsRelevant) numRelevantDefense++;
            if (b.Defense.IsRelevant) numRelevantDefense++;

            int numRequiredIVs = 0;
            if (numRelevantHP > 0) numRequiredIVs++;
            if (numRelevantAttack > 0) numRequiredIVs++;
            if (numRelevantDefense > 0) numRequiredIVs++;

            if (numRequiredIVs == 0) return 1.0f;

            // base probability is the chance of getting the IV categories we want
            float result = GameConstants.IVDesiredProbabilities[numRequiredIVs - 1];

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
