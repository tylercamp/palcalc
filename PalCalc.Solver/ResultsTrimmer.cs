using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    // takes a list of results and trims it to a smaller list, preserving at
    // least one of each "primary" property, and reapplies result pruning
    // within these new bands of properties
    //
    // - banding of steps, traits is straightforwrd
    // - banding of locations is just by number of location types, not including owner player

    public interface ITrimBanding
    {
        public int BandingHashOf(IPalReference palRef);
    }

    public class NumBreedingStepsBanding : ITrimBanding
    {
        public int BandingHashOf(IPalReference palRef) => palRef.NumTotalBreedingSteps;
    }

    public class TraitsBanding : ITrimBanding
    {
        public int BandingHashOf(IPalReference palRef) => palRef.EffectiveTraitsHash;
    }

    public class LocationTypeBanding : ITrimBanding
    {
        public int BandingHashOf(IPalReference palRef) =>
            palRef
                .AllReferences()
                .GroupBy(r => r.Location.GetType())
                .ToDictionary(g => g.Key, g => g.ToList().SetHash())
                .SetHash();
    }

    public class ResultsTrimmer
    {
        Func<IPalReference, int> BandingFunc;
        PruningRulesBuilder prb;

        public ResultsTrimmer(IEnumerable<ITrimBanding> bandingMethods, PruningRulesBuilder bandPruningBuilder)
        {
            BandingFunc = palRef => bandingMethods.Aggregate(0, (hash, banding) => HashCode.Combine(hash, banding.BandingHashOf(palRef)));

            this.prb = bandPruningBuilder;
        }

        public IEnumerable<IPalReference> Trim(IEnumerable<IPalReference> initial, CancellationToken token)
        {
            var pruners = prb.Build(token);
            return initial
                .GroupBy(BandingFunc)
                .SelectMany(g => pruners.Aggregate(g.AsEnumerable(), (v, p) => p.Apply(v)));
        }

        public static ResultsTrimmer Default => new(
            [ new NumBreedingStepsBanding(), new TraitsBanding(), new LocationTypeBanding() ],
            PruningRulesBuilder.Default
        );
    }
}
