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

        int NumTotalBreedingSteps { get; }

        string EffectivePassivesString => EffectivePassives.PassiveSkillListToString();

        IPalRefLocation Location { get; }

        TimeSpan BreedingEffort { get; }
        TimeSpan SelfBreedingEffort { get; }

        IPalReference WithGuaranteedGender(PalDB db, PalGender gender);

        bool IsCompatibleGender(PalGender otherGender) => Gender == PalGender.WILDCARD || Gender != otherGender;
    }
}
