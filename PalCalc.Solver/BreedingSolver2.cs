using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public class BreedingSolver2(BreedingSolverSettings settings)
    {
        private PalBreedingDB breedingdb = PalBreedingDB.LoadEmbedded(settings.DB);

        /*
         * The original impl. did an all-pairs calculation. It constantly compared costs and pruned
         * inefficient results to keep the runtime manageable. It had a broad calculation phase, followed
         * by a consolidation phase.
         * 
         * This new impl. has a more "directed" process:
         * 
         * 1. BFS on pal attributes, prioritizes finding new ways to produce pals
         * 2. 
         */

        private List<(Pal, Pal)> GatherRelevantPalPairs(Pal target)
        {
            var result = new HashSet<(Pal, Pal)>();
            var visited = new HashSet<Pal>();
            var toCheck = new Stack<Pal>();
            toCheck.Push(target);

            while (toCheck.TryPop(out var pal))
            {
                foreach (var (parent1, others) in breedingdb.BreedingByChild[pal])
                {
                    if (!visited.Contains(parent1.Pal)) toCheck.Push(parent1.Pal);
                    foreach (var parent2 in others)
                    {
                        if (!visited.Contains(parent2.Pal)) toCheck.Push(parent2.Pal);

                        var p1 = parent1.Pal;
                        var p2 = parent2.Pal;

                        if (p1.Id.CompareTo(p2.Id) > 0)
                            (p1, p2) = (p2, p1);

                        result.Add((p1, p2));
                    }
                }
            }

            return result.ToList();
        }

        //public List<IPalReference> SolveFor(PalSpecifier spec, SolverStateController controller)
        //{
        //    var relevantPalPairs = GatherRelevantPalPairs(spec.Pal);


        //}
    }
}
