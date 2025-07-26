using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class MinimumBreedingStepsPruning : IResultPruning
    {
        public MinimumBreedingStepsPruning(CancellationToken token) : base(token)
        {
        }

        private static int NumBreedingSteps(IPalReference r)
        {
            int count = 0;
            var observedRefs = new HashSet<int>();
            foreach (var p in r.AllReferences())
            {
                if (p is BredPalReference)
                {
                    var hash = p.GetHashCode();
                    if (!observedRefs.Contains(hash))
                    {
                        observedRefs.Add(hash);
                        count++;
                    }
                }
            }

            return count;
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            MinGroupOf(results, NumBreedingSteps);
    }
}
