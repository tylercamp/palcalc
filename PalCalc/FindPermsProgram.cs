using PalCalc.model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
{
    internal class FindPermsProgram
    {
        static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            var db = PalDB.FromJson(File.ReadAllText("db.json"));
            Console.WriteLine("Loaded Pal DB");
            var savedInstances = PalInstance.JsonToList(db, File.ReadAllText("savegame.json"));
            Console.WriteLine("Loaded save game");

            // !!! CONFIG !!!
            var ALLOW_MULTI_ROOT = true;
            var MAX_WILD_PALS = 0;
            var MAX_BREEDING_STEPS = 5; // MAX VALUE OF 5; ANY HIGHER WILL USE ALL MEMORY (though this depends on which pal you're targetting)

            var targetInstance = new PalInstance
            {
                Pal = "Suzaku".ToPal(db),
                Gender = PalGender.WILDCARD,
                Traits = new List<Trait> { "Swift".ToTrait(db), "Runner".ToTrait(db), "Nimble".ToTrait(db) },
                Location = null
            };

            /*
             * Given the set of available pals with traits:
             * 
             * 1. For each available (pal, gender) pair, and for each set of instance traits as a subset of the desired traits (e.g. all male lamballs with "runner", all with "runner and swift", etc.),
             *    pick the instance with the fewest total traits
             *
             *    (These are the instances we'll use for graph building)
             * 
             * 2. Find all paths from each pal with a desired trait(s) to the final target pal
             *   2.1. Filter edges to "valid" breedings based on available pals in save; require (node,edge) pairs with opposite
             *        gender
             *   2.2. Allow a limited number of traitless wildcard nodes (i.e. "we can use this path, but you'll need to catch these extra Pals")
             *   2.3. Limit to some maximum number of edges via "search limit"(*2) (breadth-first search of up to N nodes)
             * 
             * 4. Build a dictionary where, for any pal with a specific gender and set of traits, we can find the set of paths to that instance.
             * 
             */

            var relevantPals = PalCalcUtils.RelevantInstancesForTraits(db, savedInstances, targetInstance.Traits);

            Console.WriteLine(
                "Using {0}/{1} pals as relevant inputs with traits:\n- {2}",
                relevantPals.Count,
                savedInstances.Count,
                string.Join("\n- ",
                    relevantPals
                        .OrderBy(p => p.Pal.Name)
                        .ThenBy(p => p.Gender)
                        .ThenBy(p => string.Join(" ", p.Traits.OrderBy(t => t.Name)))
                )
            );

            // `relevantPals` is now a list of all captured Pal types, where multiple of the same pal
            // may be included if they have different genders and/or different matching subsets of
            // the desired traits
            
            List<IPalReference> availablePalsInstances = new List<IPalReference>(relevantPals.Select(i => new OwnedPalReference(i)));
            if (MAX_WILD_PALS > 0) {
                availablePalsInstances.AddRange(db.Pals.Where(p => !relevantPals.Any(i => i.Pal == p)).Select(p => new WildcardPalReference(p)));
            }

            Console.WriteLine("Using {0} pals for graph search:\n- {1}", availablePalsInstances.Count, string.Join("\n- ", availablePalsInstances));

            var palDistances = PalCalcUtils.CalcMinDistances(db);
            var availableTraitsByPal = db.Pals.ToDictionary(p => p, p => availablePalsInstances.Where(inst => inst.Pal == p).SelectMany(inst => inst.Traits).Distinct().ToList());

            Dictionary<Pal, List<List<BreedingResult>>> FindPaths()
            {
                var ALL_BREEDINGS = db.Breeding.SelectMany(b => b.Parents.Select(p => (b, p))).Distinct().GroupBy(p => p.p).ToDictionary(g => g.Key, g => g.ToDictionary(p => p.b.OtherParent(g.Key), p => p.b));

                var maxRelevantTraitsPerPal = availableTraitsByPal.MaxBy(t => t.Value.Count);
                var pathsCache = new ConcurrentDictionary<(string, Pal, int), List<List<BreedingResult>>>();
                var unreachableCache = new ConcurrentDictionary<(int, Pal), int>();
                return availablePalsInstances
                    .Select(p => p.Pal)
                    .Distinct()
                    .AsParallel()
                    .Select(pal =>
                    {
                        IEnumerable<IEnumerable<BreedingResult>> FindPathsToTarget(IEnumerable<Pal> previousNodes, Pal startNode, int maxDepth)
                        {
                            if (
                                palDistances[startNode][targetInstance.Pal] <= maxDepth + 1 && // not sure why we have off-by-one error?
                                !unreachableCache.ContainsKey((maxDepth, startNode))
                            )
                            {
                                bool didReach = false;
                                var otherParentResults = db.BreedingByParent[startNode];
                                foreach (var kvp in otherParentResults)
                                {
                                    var otherParent = kvp.Key;
                                    var child = kvp.Value.Child;
                                    var asResult = ALL_BREEDINGS[startNode][otherParent];
                                    var traitsThusFar = previousNodes.Concat(asResult.Parents).SelectMany(p => availableTraitsByPal[p]).Distinct().Intersect(targetInstance.Traits).OrderBy(s => s.ToString()).ToList();
                                    if (child == targetInstance.Pal)
                                    {
                                        didReach = true;

                                        // (trait-based comparisons not very accurate, will have false-positives, since we're not distinguishing
                                        // between input pal and child pal from previous steps, but still allows us to trim down the total list
                                        // of options without causing false-negatives)
                                        if (
                                            targetInstance.Traits.Count == 0 ||
                                            (
                                                ALLOW_MULTI_ROOT
                                                    // only bother returning this option if we would have visited at least 1 required trait
                                                    // (we may miss some possible options from multi-root search later on, but they should be
                                                    // negligible compared to the total results we'll still be getting)
                                                    ? traitsThusFar.Count > 0
                                                    // only return options which satisfy all of the requested traits
                                                    : traitsThusFar.Count == targetInstance.Traits.Count
                                            )
                                        ) yield return new List<BreedingResult>() { asResult };
                                    }
                                    else if (maxDepth > 0)
                                    {
                                        bool useCache = false;

                                        var traitsThusFarStr = string.Join(" ", traitsThusFar.Concat(availableTraitsByPal[child]).Distinct().Intersect(targetInstance.Traits).Select(t => t.ToString()).OrderBy(s => s));
                                        var nextDepth = maxDepth - 1;
                                        var wasCached = useCache && pathsCache.ContainsKey((traitsThusFarStr, child, nextDepth));
                                        var subPaths = wasCached
                                            ? pathsCache[(traitsThusFarStr, child, nextDepth)]
                                            : FindPathsToTarget(previousNodes.Append(startNode), child, nextDepth);

                                        if (useCache && !wasCached) pathsCache.TryAdd((traitsThusFarStr, child, nextDepth), subPaths.Select(p => p.ToList()).ToList());

                                        foreach (var path in subPaths)
                                        {
                                            didReach = true;
                                            yield return Enumerable.Prepend(path, asResult);
                                        }
                                    }
                                }

                                //if (!didReach)
                                //    unreachableCache.TryAdd((maxDepth-1, startNode), 0);
                            }
                        }

                        // (the call to `FindPathsToTarget` invokes a breeding step, allow it to produce MAX-1 further steps)
                        var result = FindPathsToTarget(Enumerable.Empty<Pal>(), pal, MAX_BREEDING_STEPS - 1).Select(p => p.ToList()).ToList();
                        Console.WriteLine("Finished search from {0}", pal);
                        return (pal, result);
                    })
                    .ToList()
                    .ToDictionary(r => r.pal, r => r.result);
            }

            var pathsToTargetByPal = FindPaths();
            Console.WriteLine("Found {0} paths to {1}", pathsToTargetByPal.Sum(kvp => kvp.Value.Count), targetInstance.Pal);

            var orderedPathsByMinTraits = pathsToTargetByPal
                .SelectMany(kvp => kvp.Value)
                .Select(pathList =>
                {
                    var db2 = db;
                    var parents = pathList[0].Parents;
                    var intermediates = new List<Pal>();
                    var lastBreed = pathList[0];
                    foreach (var breed in pathList.Skip(1))
                    {
                        intermediates.Add(breed.OtherParent(lastBreed.Child));
                        lastBreed = breed;
                    }

                    var path = new BreedingPath
                    {
                        Parent1 = pathList[0].Parent1,
                        Parent2 = pathList[0].Parent2,
                        Intermediates = intermediates,
                        Result = lastBreed.Child
                    };
                    return path;
                })
                .Where(path => path.Participants.All(p => availablePalsInstances.Any(i => i.Pal == p)))
                .Where(path => !targetInstance.Traits.Except(path.Participants.SelectMany(p => availableTraitsByPal[p])).Any())
                .OrderBy(path => path.Participants.SelectMany(pal => availableTraitsByPal[pal]).Distinct().Count())
                .ThenBy(path => path.Participants.Count())
                .ToList();

            var solvedResults = new List<(BreedingPath, List<IPalReference>)>();
            foreach (var path in orderedPathsByMinTraits)
            {
                var resultingTraits = path.Participants.SelectMany(pal => availableTraitsByPal[pal]).Distinct().ToList();

                var availableInstancesByPal = path.Participants.Distinct().ToDictionary(p => p, p => availablePalsInstances.Where(i => i.Pal == p).ToList());
                if (path.Participants.Any(p => !availableInstancesByPal.ContainsKey(p))) continue;

                var definiteInstances = new List<IPalReference>();
                definiteInstances.AddRange(availableInstancesByPal.Values.Where(il => il.Count == 1).Select(il => il.Single()));

                {
                    var resolved = new List<Pal>();
                    foreach (var pal in availableInstancesByPal.Keys)
                    {
                        if (availableInstancesByPal[pal].Count == 0 || definiteInstances.Count(i => i.Pal == pal) == path.Participants.Count(p => p == pal))
                            resolved.Add(pal);
                    }
                    foreach (var p in resolved) availableInstancesByPal.Remove(p);
                }

                var resolvedTraits = targetInstance.Traits.Intersect(definiteInstances.SelectMany(i => i.Traits).Distinct()).ToList();
                var remainingTraits = targetInstance.Traits.Except(resolvedTraits).ToList();

                if (remainingTraits.Count == 0) break;

                foreach (var pal in availableInstancesByPal.Keys.ToList())
                {
                    availableInstancesByPal[pal] = availableInstancesByPal[pal].Where(i => i.Traits.Intersect(remainingTraits).Any()).ToList();
                }

                // for remaining pals, consider each instance and find the set of other valid pals
                IEnumerable<List<IPalReference>> GetValidCoparticipants(List<Trait> currentTraits, IPalReference basePal, IEnumerable<IEnumerable<IPalReference>> possibleCoparticipants)
                {
                    var newCurrentTraits = currentTraits.Concat(basePal.Traits).Distinct().ToList();

                    if (!targetInstance.Traits.Except(newCurrentTraits).Any())
                    {
                        if (!possibleCoparticipants.Any())
                        {
                            // we've satisfied the requirements and there aren't any other participants we have to search for
                            yield return new List<IPalReference>() { basePal };
                        }
                    }
                    else if (possibleCoparticipants.Any())
                    {
                        foreach (var participant in possibleCoparticipants.First())
                        {
                            // check the set of participants for the next pal and return any valid sets
                            foreach (var coparticipants in GetValidCoparticipants(newCurrentTraits, participant, possibleCoparticipants.Skip(1)))
                                yield return Enumerable.Prepend(coparticipants, basePal).ToList();
                        }
                    }
                }

                var possibleCoparticipants = path.Participants.Except(definiteInstances.Select(i => i.Pal)).Select(p => availableInstancesByPal[p]);
                var optionsForRemainingTraits = possibleCoparticipants.First().SelectMany(ir => GetValidCoparticipants(resolvedTraits, ir, possibleCoparticipants.Skip(1))).ToList();

                var orderedOptions = optionsForRemainingTraits
                    .Select(opts => opts.Concat(definiteInstances).ToList())
                    .Where(opts =>
                    {
                        var parent1Genders = opts.Where(o => o.Pal == path.Parent1).Select(i => i.Gender);
                        var parent2Genders = opts.Where(o => o.Pal == path.Parent2).Select(i => i.Gender);
                        return parent1Genders.Any(g1 => parent2Genders.Any(g2 => g1 != g2));
                    })
                    .Where(l => !targetInstance.Traits.Except(l.SelectMany(i => i.Traits)).Any())
                    .Where(l => l.Count(i => i is WildcardPalReference) <= MAX_WILD_PALS)
                    // prefer options with the fewest number of irrelevant traits
                    .OrderBy(l => l.SelectMany(ir => ir.Traits.Except(targetInstance.Traits)).Count())
                    // then prefer options where the involved pals contain multiple of any of the desired traits
                    .ThenBy(l => l.SelectMany(ir => ir.Traits.Intersect(targetInstance.Traits)).Count())
                    // then prefer options where any irrelevant traits are early in the chain
                    .ThenBy(l => l.Zip(Enumerable.Range(1, l.Count).Select(i => i * 100)).Sum(pair => pair.Second * pair.First.Traits.Except(targetInstance.Traits).Count()))
                    .ToList();

                if (!orderedOptions.Any()) continue;

                var bestOption = new List<IPalReference>(orderedOptions.First());
                solvedResults.Add((path, bestOption));
            }

            solvedResults = solvedResults
                // prefer options with the fewest number of irrelevant traits
                .OrderBy(pair => pair.Item2.SelectMany(ir => ir.Traits.Except(targetInstance.Traits)).Count())
                // prefer options where the involved pals contain multiple of any of the desired traits
                .ThenByDescending(pair => pair.Item2.SelectMany(ir => ir.Traits.Intersect(targetInstance.Traits)).Count())
                // prefer options where any irrelevant traits are early in the chain
                .ThenBy(pair => pair.Item2.Zip(Enumerable.Range(1, pair.Item2.Count).Select(i => i * 100)).Sum(pair => pair.Second * pair.First.Traits.Except(targetInstance.Traits).Count()))
                // order by shortest path
                .ThenBy(pair => pair.Item1.NumBreeds)
                // order by fewest uses of wild/captured pals
                .ThenBy(pair => pair.Item2.Count(i => i is WildcardPalReference))
                .ToList();

            Console.Clear();

            foreach (var r in solvedResults)
            {
                var path = r.Item1;
                var bestOption = r.Item2;

                Console.WriteLine($"Path of {path.NumBreeds} steps with {bestOption.SelectMany(o => o.Traits).Distinct().Count()} final possible traits");
                foreach (var br in path.AsBreedingResults(db))
                    Console.WriteLine("- {0}", br);

                Console.WriteLine("Using:");
                foreach (var i in bestOption)
                    Console.WriteLine("- {0}", i);

                Console.WriteLine("=====");
            }

            Console.WriteLine("Took {0}", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds));
        }
    }
}
