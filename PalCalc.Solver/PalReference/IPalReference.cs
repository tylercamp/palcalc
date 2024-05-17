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
        /// The list of DESIRED traits held by this pal. Any irrelevant traits are to
        /// be represented as a Random trait.
        /// </summary>
        List<Trait> EffectiveTraits { get; }
        int EffectiveTraitsHash { get; } // optimization

        List<Trait> ActualTraits { get; }

        PalGender Gender { get; }

        int NumTotalBreedingSteps { get; }

        string EffectiveTraitsString => EffectiveTraits.TraitsListToString();

        IPalRefLocation Location { get; }

        TimeSpan BreedingEffort { get; }
        TimeSpan SelfBreedingEffort { get; }

        IPalReference WithGuaranteedGender(PalDB db, PalGender gender);

        bool IsCompatibleGender(PalGender otherGender) => Gender == PalGender.WILDCARD || Gender != otherGender;
    }
}
