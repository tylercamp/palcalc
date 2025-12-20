using PalCalc.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    public interface IPalReference
    {
        Pal Pal { get; }

        /// <summary>
        /// The list of DESIRED passives held by this pal. Any irrelevant passives are to
        /// be represented as a Random passive.
        /// </summary>
        List<PassiveSkill> EffectivePassives { get; }

        // (note: should be based on the effective passives names, not on the effective passives themselves. we
        // want random passives to be distinct from each other for passive-list-dedup purposes, but not for set-hash,
        // since that would prevent random-passive pals from being grouped together during pruning.)
        int EffectivePassivesHash { get; } // optimizations

        IV_Set IVs { get; }

        List<PassiveSkill> ActualPassives { get; }

        PalGender Gender { get; }

        /// <summary>
        /// Meant for `Philanthropist` passive, which halves the time required to produce a breeding result when this
        /// is used as a parent. These effects are multiplicative, e.g. 1 with Philanthropist gives 0.5x total factor,
        /// both with Philanthropist gives 0.5*0.5 = 0.25x total factor
        /// </summary>
        float TimeFactor { get; }

        string EffectivePassivesString => EffectivePassives.PassiveSkillListToString();

        IPalRefLocation Location { get; }

        TimeSpan BreedingEffort { get; }
        TimeSpan SelfBreedingEffort { get; }
        int TotalCost { get; }

        /// <summary>
        /// Returns a version of this pal with the given gender. The new pal may have its effort updated
        /// to reflect gender probabilities, depending on `useReverser` (which makes gender-change free).
        /// </summary>
        IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser);

        bool IsCompatibleGender(PalGender otherGender) => Gender == PalGender.WILDCARD || Gender != otherGender;



        /* Small, pre-computed properties used for result pruning (try and minimize calls to `.AllReferences()` ext. method */
        int NumTotalBreedingSteps { get; }

        int NumTotalEggs { get; }

        int NumTotalWildPals { get; }
    }
}
