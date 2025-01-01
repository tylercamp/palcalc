using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    internal class BreedingDistanceMap
    {
        private static ILogger logger = Log.ForContext<BreedingDistanceMap>();

        // min. number of times you need to breed Key1 to get a Key2 (to prune out path checks between pals which would exceed the max breeding steps)
        public static Dictionary<Pal, Dictionary<Pal, int>> CalcMinDistances(PalDB db, PalBreedingDB breedingdb)
        {
            Logging.InitCommonFull();

            Dictionary<Pal, Dictionary<Pal, int>> palDistances = new Dictionary<Pal, Dictionary<Pal, int>>();

            foreach (var p in db.Pals)
                palDistances.Add(p, new Dictionary<Pal, int>() { { p, 0 } });

            List<(Pal, Pal)> toCheck = new List<(Pal, Pal)>(db.Pals.SelectMany(p => db.Pals.Where(i => i != p).Select(p2 => (p, p2))));
            bool didUpdate = true;

            while (didUpdate)
            {
                didUpdate = false;

                List<(Pal, Pal)> resolved = new List<(Pal, Pal)>();
                List<(Pal, Pal)> unresolved = new List<(Pal, Pal)>();
                foreach (var next in toCheck)
                {
                    var src = next.Item1;
                    var target = next.Item2;

                    // check if there's a direct way to breed from src to target
                    if (breedingdb.BreedingByChild[target].Any(kvp => kvp.Key.Pal == src))
                    {
                        if (!palDistances[src].ContainsKey(target) || palDistances[src][target] != 1)
                        {
                            didUpdate = true;
                            palDistances[src][target] = 1;
                            resolved.Add(next);
                        }
                        continue;
                    }

                    // check if there's a possible child of this `src` with known distance to target
                    var childWithShortestDistance = breedingdb.BreedingByParent[src].Values
                        .SelectMany(l => l.Select(b => b.Child))
                        .Where(child => palDistances[child].ContainsKey(target))
                        .OrderBy(child => palDistances[child][target])
                        .FirstOrDefault();

                    if (childWithShortestDistance != null)
                    {
                        if (!palDistances[src].ContainsKey(target) || palDistances[src][target] != palDistances[childWithShortestDistance][target] + 1)
                        {
                            didUpdate = true;
                            palDistances[src][target] = palDistances[childWithShortestDistance][target] + 1;
                            resolved.Add(next);
                        }
                        continue;
                    }

                    unresolved.Add(next);
                }

                logger.Information("Resolved {0} entries with {1} left unresolved", resolved.Count, unresolved.Count);

                if (!didUpdate)
                {
                    // the remaining (src,target) pairs are impossible
                    foreach (var p in unresolved)
                    {
                        palDistances[p.Item1].Add(p.Item2, 10000);
                    }
                }
            }

            return palDistances;
        }
    }
}
