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
        int EffectivePassivesHash { get; } // optimization

        IV_IValue IV_HP { get; }
        IV_IValue IV_Attack { get; }
        IV_IValue IV_Defense { get; }

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
