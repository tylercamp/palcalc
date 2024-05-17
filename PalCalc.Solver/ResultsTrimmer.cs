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

    public class ResultsTrimmer
    {
        private PruningRulesBuilder prb;
        private CancellationToken token;
        IEnumerable<IResultPruning> pruners;
        public ResultsTrimmer(PruningRulesBuilder prb, CancellationToken token)
        {
            this.prb = prb;
            this.pruners = prb.Build(token);
        }

        public IEnumerable<IPalReference> Trim(IEnumerable<IPalReference> initial) => initial
            .GroupBy(pr => (
                pr.NumTotalBreedingSteps,
                pr.EffectiveTraitsHash,
                pr
                    .AllReferences()
                    .GroupBy(r => r.Location.GetType())
                    .ToDictionary(g => g.Key, g => g.ToList().SetHash())
                    .SetHash()
            ))
            .SelectMany(g => pruners.Aggregate(g.AsEnumerable(), (v, p) => p.Apply(v)));
    }
}
