using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
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
        FPassiveSet EffectivePassives { get; }

        FPassiveSet ActualPassives { get; }

        FIVSet IVs { get; }

        PalGender Gender { get; }

        /// <summary>
        /// Meant for `Philanthropist` passive, which halves the time required to produce a breeding result when this
        /// is used as a parent. These effects are multiplicative, e.g. 1 with Philanthropist gives 0.5x total factor,
        /// both with Philanthropist gives 0.5*0.5 = 0.25x total factor
        /// </summary>
        float TimeFactor { get; }

        IPalRefLocation Location { get; }

        TimeSpan BreedingEffort { get; }
        TimeSpan SelfBreedingEffort { get; }

        IPalReference WithGuaranteedGender(PalDB db, PalGender gender);

        bool IsCompatibleGender(PalGender otherGender) => Gender == PalGender.WILDCARD || Gender != otherGender;


        /* Small, pre-computed properties used for result pruning (try and minimize calls to `.AllReferences()` ext. method */
        int NumTotalBreedingSteps { get; }

        int NumTotalEggs { get; }

        int NumTotalWildPals { get; }
    }
}
