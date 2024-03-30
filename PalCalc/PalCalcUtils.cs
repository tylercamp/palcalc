using PalCalc.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
{
    internal class PalCalcUtils
    {
        // min. number of times you need to breed Key1 to get a Key2 (to prune out path checks between pals which would exceed the max breeding steps)
        public static Dictionary<Pal, Dictionary<Pal, int>> CalcMinDistances(PalDB db)
        {
            Dictionary<Pal, Dictionary<Pal, int>> palDistances = new Dictionary<Pal, Dictionary<Pal, int>>();

            foreach (var p in db.Pals)
                palDistances.Add(p, new Dictionary<Pal, int>() { { p, 0 } });

            {
                List<(Pal, Pal)> toCheck = new List<(Pal, Pal)>(db.Pals.SelectMany(p => db.Pals.Where(i => i != p).Select(p2 => (p, p2))));
                bool didUpdate = true;

                while (didUpdate)
                {
                    didUpdate = false;

                    List<(Pal, Pal)> resolved = new List<(Pal, Pal)>();
                    List<(Pal, Pal)> unresolved = new List<(Pal, Pal)>();
                    Console.WriteLine("{0} distances left to check", toCheck.Count);
                    foreach (var next in toCheck)
                    {
                        var src = next.Item1;
                        var target = next.Item2;

                        // check if there's a direct way to breed from src to target
                        if (db.BreedingByChild[target].ContainsKey(src))
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
                        var childWithShortestDistance = db.BreedingByParent[src].Values.Select(b => b.Child).Where(child => palDistances[child].ContainsKey(target)).OrderBy(child => palDistances[child][target]).FirstOrDefault();
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

                    Console.WriteLine("Resolved {0} entries with {1} left unresolved", resolved.Count, unresolved.Count);

                    if (!didUpdate)
                    {
                        // the remaining (src,target) pairs are impossible (?)
                        foreach (var p in unresolved)
                        {
                            palDistances[p.Item1].Add(p.Item2, 10000);
                        }
                    }
                }
            }

            return palDistances;
        }


        // for each available (pal, gender) pair, and for each set of instance traits as a subset of the desired traits (e.g. all male lamballs with "runner",
        // all with "runner and swift", etc.), pick the instance with the fewest total traits
        //
        // (includes pals without any relevant traits, picks the instance with the fewest total traits)
        public static List<PalInstance> RelevantInstancesForTraits(PalDB db, List<PalInstance> availableInstances, List<Trait> targetTraits)
        {
            List<PalInstance> relevantInstances = new List<PalInstance>();

            var traitPermutations = targetTraits.Combinations(targetTraits.Count).Select(l => l.ToList()).ToList();
            Console.WriteLine("Looking for pals with traits:\n- {0}", string.Join("\n- ", traitPermutations.Select(p => $"({string.Join(',', p)})")));

            foreach (var pal in db.Pals)
                foreach (var gender in new List<PalGender>() { PalGender.MALE, PalGender.FEMALE })
                {
                    var instances = availableInstances.Where(i => i.Pal == pal && i.Gender == gender).ToList();
                    var instancesByPermutation = traitPermutations.ToDictionary(p => p, p => new List<PalInstance>());

                    foreach (var instance in instances)
                    {
                        var matchingPermutation = traitPermutations.OrderByDescending(p => p.Count).ThenBy(p => p.Except(instance.Traits).Count()).First(p => !p.Except(instance.Traits).Any());
                        instancesByPermutation[matchingPermutation].Add(instance);
                    }

                    relevantInstances.AddRange(instancesByPermutation.Values.Where(instances => instances.Any()).Select(instances => instances.OrderBy(i => i.Traits.Count).First()));
                }

            return relevantInstances;
        }
    }
}
