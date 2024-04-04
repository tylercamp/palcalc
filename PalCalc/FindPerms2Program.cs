using PalCalc.model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
{
    interface IBreedingTreeNode
    {
        IPalReference PalRef { get; }
        IEnumerable<IBreedingTreeNode> Children { get; }

        IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth);

        IEnumerable<string> DescriptionLines { get; }
    }

    class DirectPalNode : IBreedingTreeNode
    {
        public DirectPalNode(IPalReference pref)
        {
            PalRef = pref;
        }

        public IPalReference PalRef { get; }
        public IEnumerable<IBreedingTreeNode> Children { get; } = Enumerable.Empty<IBreedingTreeNode>();

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            yield return (this, currentDepth);
        }

        public IEnumerable<String> DescriptionLines
        {
            get
            {
                switch (PalRef)
                {
                    case WildcardPalReference wild:
                        yield return $"Wild {wild.Pal.Name}";
                        yield return $"{wild.Gender} gender w/ up to {wild.Traits.Count} random traits";
                        break;

                    case OwnedPalReference owned:
                        yield return $"Owned {owned.Pal.Name}";
                        yield return $"in {owned.Location}";
                        yield return $"{owned.Gender} w/ {owned.Traits.TraitsListToString()}";
                        break;

                    default: throw new NotImplementedException();
                }
            }
        }
    }

    class BredPalNode : IBreedingTreeNode
    {
        public BredPalNode(BredPalReference bpr, IBreedingTreeNode parentNode1, IBreedingTreeNode parentNode2)
        {
            PalRef = bpr;
            Children = new List<IBreedingTreeNode>() { parentNode1, parentNode2 };

            ParentNode1 = parentNode1;
            ParentNode2 = parentNode2;
        }

        public IBreedingTreeNode ParentNode1 { get; }
        public IBreedingTreeNode ParentNode2 { get; }

        public IPalReference PalRef { get; }

        public IEnumerable<IBreedingTreeNode> Children { get; }

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            foreach (var n in ParentNode1.TraversedTopDown(currentDepth + 1))
                yield return n;

            yield return (this, currentDepth);

            foreach (var n in ParentNode2.TraversedTopDown(currentDepth + 1))
                yield return n;
        }

        public IEnumerable<string> DescriptionLines
        {
            get
            {
                var asBred = PalRef as BredPalReference;
                yield return $"Bred {asBred.Pal.Name}";
                yield return $"{asBred.Gender} gender w/ {asBred.Traits.TraitsListToString()}";
                yield return $"takes ~{asBred.SelfBreedingEffort} for {asBred.AvgRequiredBreedings} breed attempts";
            }
        }
    }

    class BreedingTree
    {
        IBreedingTreeNode BuildNode(IPalReference pref)
        {
            switch (pref)
            {
                case BredPalReference bpr:
                    return new BredPalNode(bpr, BuildNode(bpr.Parent1), BuildNode(bpr.Parent2));

                case WildcardPalReference:
                case OwnedPalReference:
                    return new DirectPalNode(pref);

                default: throw new NotImplementedException();
            }
        }

        public BreedingTree(IPalReference finalPal)
        {
            Root = BuildNode(finalPal);
        }

        public IBreedingTreeNode Root { get; }

        public IEnumerable<(IBreedingTreeNode, int)> AllNodes => Root.TraversedTopDown(0);

        public void Print()
        {
            /*
             * Node
             * Description
             *       \
             *              Node
             *              Description
             *       /
             * Node
             * Description
             */

            var maxDescriptionLengthByDepth = AllNodes.GroupBy(n => n.Item2).ToDictionary(g => g.Key, g => g.Max(p => p.Item1.DescriptionLines.Max(l => l.Length)));
            var maxDepth = maxDescriptionLengthByDepth.Keys.Max();

            var indentationByDepth = Enumerable
                .Range(0, maxDepth + 1)
                .ToDictionary(
                    depth => depth,
                    depth =>
                    {
                        if (depth == maxDepth) return 0;

                        var priorDepthsLengths = Enumerable.Range(1, maxDepth - depth).Select(depthOffset => 1 + maxDescriptionLengthByDepth[depth + depthOffset]).ToList();
                        return priorDepthsLengths.Sum();
                    }
                );

            int? prevDepth = null;
            foreach (var (node, depth) in AllNodes)
            {
                var indentation = new string(' ', indentationByDepth[depth]);

                if (prevDepth != null)
                {
                    if (prevDepth > depth)
                        Console.WriteLine("{0}\\", indentation);
                    else if (prevDepth < depth)
                        Console.WriteLine("{0}/", new string(' ', indentationByDepth[prevDepth.Value]));
                }

                foreach (var line in node.DescriptionLines)
                    Console.WriteLine("{0} {1}", indentation, line);

                prevDepth = depth;
            }
        }
    }

    internal class FindPerms2Program
    {
        // N choose K -> look up column K at row N of pascal's triangle
        class PascalsTriangle
        {
            public PascalsTriangle(int numRows)
            {
                AddRows(numRows);
            }

            private List<int[]> rows = new List<int[]> { new int[] { 1 }, new int[] { 1, 1 } };

            private void AddRows(int count)
            {
                var lastRow = rows.Last();
                for (int i = 0; i < count; i++)
                {
                    var newRow = new int[lastRow.Length + 1];
                    for (int c = 0; c < newRow.Length; c++)
                    {
                        if (c == 0 || c == newRow.Length - 1) newRow[c] = 1;
                        else newRow[c] = lastRow[c] + lastRow[c - 1];
                    }
                    rows.Add(newRow);
                    lastRow = newRow;
                }
            }

            public int[] this[int row]
            {
                get
                {
                    if (this.rows.Count <= row) AddRows(row - this.rows.Count + 1);
                    return rows[row];
                }
            }

            public static PascalsTriangle Instance = new PascalsTriangle(20);
        }

        // returns number of ways you can choose k combinations from a list of n
        // TODO - is this the right way to use pascal's triangle??
        static int Choose(int n, int k) => PascalsTriangle.Instance[n - 1][k - 1];

        static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            var db = PalDB.FromJson(File.ReadAllText("db.json"));
            Console.WriteLine("Loaded Pal DB");
            var savedInstances = PalInstance.JsonToList(db, File.ReadAllText("savegame.json"));
            Console.WriteLine("Loaded save game");

            
            
            // !!! CONFIG !!!
            var MAX_WILD_PALS = 1;
            var MAX_BREEDING_STEPS = 20;

            // max num. irrelevant traits from any parents or children involved in the final breeding steps (including target pal)
            // (lower value runs faster, but considers fewer possibilities)
            var MAX_IRRELEVANT_TRAITS = 0;

            /* effort in estimated time to get the desired pal w/ traits
             * 
             * - goes by constant breeding time
             * - ignores hatching time
             * - roughly estimates time to catch wild pals with increasing time based on paldex number
            */
            var MAX_EFFORT = TimeSpan.FromDays(7);
            // !!! !!!

            var targetInstance = new PalInstance
            {
                Pal = "Ragnahawk".ToPal(db),
                Gender = PalGender.WILDCARD,
                Traits = new List<Trait> { "Swift".ToTrait(db), "Runner".ToTrait(db), "Nimble".ToTrait(db) },
                Location = null
            };

            if (targetInstance.Traits.Count > GameConfig.MaxTotalTraits)
            {
                throw new Exception("Target trait count cannot exceed max number of traits for a single pal");
            }

            /*
             * Given the set of available pals with traits:
             * 
             * 1. For each available (pal, gender) pair, and for each set of instance traits as a subset of the desired traits (e.g. all male lamballs with "runner", all with "runner and swift", etc.),
             *    pick the instance with the fewest total traits
             *
             *    (These are the instances we'll start with for search)
             * 
             * 2. Iterate over the set of "available" instances, building up the set with each iteration, for MAX_BREEDING_STEPS iterations.
             * 
             *   2.1. For each combination of pal instances, calc. the child + traits and store as new set of available instances (added after
             *        going through all combinations)
             *        
             *   2.2. Filter by whether either parent can reach the target pal without exceeding the max num. breeding steps
             *        (big performance gains)
             *        
             *   2.3. Filter by number of wild pal participants (if needed)
             *   
             *   2.4. Calculate the probability of a child with the desired traits within the given MAX_IRRELEVANT_TRAITS
             *   
             *   2.5. Reduce the set of new options by deduplicating and taking the result with the lowest effort
             *        (modest performance gains)
             *        
             *   2.6. Update the working set of available pals
             *        (big performance gains with below filters)
             *      2.6.1. Insert if there's no existing available instance like it
             *      2.6.2. Skip if there's an existing similar instance with less effort
             *      2.6.3. Replace the old existing instance if it takes more effort than the discovered instance
             * 
             * 3. The "available instances" now consists of all useful instances, including breeding results. Filter for the desired
             *    pal and traits to get the available options
             */

            if (MAX_IRRELEVANT_TRAITS > 3) MAX_IRRELEVANT_TRAITS = 3;

            var relevantPals = PalCalcUtils
                .RelevantInstancesForTraits(db, savedInstances, targetInstance.Traits)
                .Where(p => p.Traits.Except(targetInstance.Traits).Count() <= MAX_IRRELEVANT_TRAITS)
                .ToList();

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

            bool WithinBreedingSteps(Pal pal, int maxSteps) => db.MinBreedingSteps[pal][targetInstance.Pal] <= maxSteps;

            HashSet<IPalReference> availablePalInstances = new HashSet<IPalReference>(relevantPals.Where(pi => WithinBreedingSteps(pi.Pal, MAX_BREEDING_STEPS)).Select(i => new OwnedPalReference(i)));
            if (MAX_WILD_PALS > 0)
            {
                foreach (
                    var wildRef in db.Pals
                        .Where(p => !relevantPals.Any(i => i.Pal == p))
                        .Where(p => WithinBreedingSteps(p, MAX_BREEDING_STEPS))
                        .SelectMany(p => Enumerable.Range(0, MAX_IRRELEVANT_TRAITS).Select(numTraits => new WildcardPalReference(p, numTraits)))
                        .Where(pi => pi.BreedingEffort <= MAX_EFFORT)
                ) availablePalInstances.Add(wildRef);
            }

            Console.WriteLine("Using {0} pals for graph search:\n- {1}", availablePalInstances.Count, string.Join("\n- ", availablePalInstances));

            int NumWildPalParticipants(IPalReference pref)
            {
                switch (pref)
                {
                    case BredPalReference bpr: return NumWildPalParticipants(bpr.Parent1) + NumWildPalParticipants(bpr.Parent2);
                    case OwnedPalReference opr: return 0;
                    case WildcardPalReference wpr: return 1;
                    default: throw new Exception($"Unhandled pal reference type {pref.GetType()}");
                }
            }

            int NumBredPalParticipants(IPalReference pref)
            {
                switch (pref)
                {
                    case BredPalReference bpr: return 1 + NumBredPalParticipants(bpr.Parent1) + NumBredPalParticipants(bpr.Parent2);
                    default: return 0;
                }
            }

            for (int s = 0; s < MAX_BREEDING_STEPS; s++)
            {
                Console.WriteLine($"Starting search step #{s+1} with {availablePalInstances.Count} relevant pals");
                var newInstances = Enumerable.Zip(availablePalInstances, Enumerable.Range(0, availablePalInstances.Count))
                    .AsParallel()
                    .SelectMany(pair =>
                    {
                        var parent1 = pair.First;
                        var idx = pair.Second;

                        var res = availablePalInstances
                            .Skip(idx + 1) // only search (p1,p2) pairs, not (p1,p2) and (p2,p1)
                            .Where(i => i.IsCompatibleGender(parent1.Gender))
                            .Where(i => i != null)
                            .Where(parent2 => NumWildPalParticipants(parent1) + NumWildPalParticipants(parent2) <= MAX_WILD_PALS)
                            .Where(parent2 =>
                            {
                                var childPal = db.BreedingByParent[parent1.Pal][parent2.Pal].Child;
                                return db.MinBreedingSteps[childPal][targetInstance.Pal] <= MAX_BREEDING_STEPS - s - 1;
                            })
                            .Where(parent2 => NumBredPalParticipants(parent1) + NumBredPalParticipants(parent2) < MAX_BREEDING_STEPS)
                            .Where(parent2 =>
                            {
                                // if we disallow any irrelevant traits, neither parents have a useful trait, and at least 1 parent
                                // has an irrelevant trait, then it's impossible to breed a child with zero total traits
                                //
                                // (child would need to have zero since there's nothing useful to inherit and we disallow irrelevant traits,
                                //  impossible to have zero since a child always inherits at least 1 direct trait if possible)
                                if (MAX_IRRELEVANT_TRAITS > 0) return true;

                                var combinedTraits = parent1.Traits.Concat(parent2.Traits);


                                var anyRelevantFromParents = targetInstance.Traits.Intersect(combinedTraits).Any();
                                var anyIrrelevantFromParents = combinedTraits.Except(targetInstance.Traits).Any();

                                return anyRelevantFromParents || !anyIrrelevantFromParents;

                            })
                            .SelectMany(parent2 =>
                            {
                                // we have two parents but don't necessarily have definite genders for them, figure out which parent should have which
                                // gender (if they're wild/bred pals) for the least overall effort (different pals have different gender probabilities)
                                List<IPalReference> ParentOptions(IPalReference parent) => parent.Gender == PalGender.WILDCARD
                                    ? new List<IPalReference>() { parent.WithGuaranteedGender(db, PalGender.MALE), parent.WithGuaranteedGender(db, PalGender.FEMALE) }
                                    : new List<IPalReference>() { parent };

                                (IPalReference, IPalReference) PreferredParentsGenders()
                                {
                                    var optionsParent1 = ParentOptions(parent1);
                                    var optionsParent2 = ParentOptions(parent2);

                                    var parentPairOptions = optionsParent1.SelectMany(p1v => optionsParent2.Where(p2v => p2v.IsCompatibleGender(p1v.Gender)).Select(p2v => (p1v, p2v))).ToList();
                                    var optimalTime = parentPairOptions.Min(pair => pair.p1v.BreedingEffort + pair.p2v.BreedingEffort);

                                    parentPairOptions = parentPairOptions.Where(pair => pair.p1v.BreedingEffort + pair.p2v.BreedingEffort == optimalTime).ToList();
                                    if (parentPairOptions.Select(pair => pair.p1v.BreedingEffort + pair.p2v.BreedingEffort).Distinct().Count() == 1)
                                    {
                                        // either there is no preference or at least 1 parent already has a specific gender
                                        if (parent2.Gender == PalGender.WILDCARD) return (parent1, parent2.WithGuaranteedGender(db, parent1.Gender.OppositeGender()));
                                        if (parent1.Gender == PalGender.WILDCARD) return (parent1.WithGuaranteedGender(db, parent2.Gender.OppositeGender()), parent2);

                                        // neither parents are wildcards
                                        return (parent1, parent2);
                                    }
                                    else
                                    {
                                        return parentPairOptions.OrderBy(p => p.p1v.BreedingEffort + p.p2v.BreedingEffort).First();
                                    }
                                }

                                var (preferredParent1, preferredParent2) = PreferredParentsGenders();

                                var parentTraits = parent1.Traits.Concat(parent2.Traits).Distinct().ToList();
                                var desiredParentTraits = targetInstance.Traits.Intersect(parentTraits).ToList();

                                var possibleResults = new List<IPalReference>();

                                var probabilityForUpToNumTraits = 0.0f;

                                // go through each potential final number of traits, accumulate the probability of any of these exact options
                                // leading to the desired traits within MAX_IRRELEVANT_TRAITS
                                for (int numFinalTraits = 0; numFinalTraits <= GameConfig.MaxTotalTraits; numFinalTraits++)
                                {
                                    // only looking for probability of getting all desired parent traits, which means we need at least Count(desired)
                                    // total traits
                                    if (numFinalTraits < desiredParentTraits.Count) continue;

                                    // exceeding Count(desiredTraits) + MAX_IRRELEVANT_TRAITS means we've exceeded the max irrelevant traits allowed
                                    if (numFinalTraits > desiredParentTraits.Count + MAX_IRRELEVANT_TRAITS) break;

                                    float initialProbability = probabilityForUpToNumTraits;

                                    for (int numInheritedFromParent = desiredParentTraits.Count; numInheritedFromParent <= numFinalTraits; numInheritedFromParent++)
                                    {
                                        // we may inherit more traits from the parents than the parents actually have (e.g. inherit 4 traits from parents with
                                        // 2 total traits), in which case we'd still inherit just two
                                        //
                                        // this doesn't affect probabilities of getting `numInherited`, but it affects the number of random traits which must
                                        // be added to each `numFinalTraits` and the number of combinations of parent traits that we check
                                        var actualNumInheritedFromParent = Math.Min(numInheritedFromParent, parentTraits.Count);
                                    
                                        var numIrrelevantFromParent = actualNumInheritedFromParent - desiredParentTraits.Count;
                                        var numIrrelevantFromRandom = numFinalTraits - (numIrrelevantFromParent + desiredParentTraits.Count);

                                        // can inherit at most 3 random traits; if this `if` is `true` then we've hit a case which would never actually happen
                                        // (e.g. 4 target final traits, 0 from parents, 4 from random)
                                        if (numIrrelevantFromRandom > 3) continue;

#if DEBUG
                                        if (numIrrelevantFromRandom < 0) Debugger.Break();
#endif

                                        float probabilityGotRequiredFromParent;
                                        if (numInheritedFromParent == 0)
                                        {
                                            // would only happen if neither parent has a desired trait

                                            // the only way we could get zero inherited traits is if neither parent actually has any traits, otherwise
                                            // it (seems to) be impossible to get zero direct inherited traits (unconfirmed from reddit thread)
                                            if (parentTraits.Count > 0) continue;

                                            // if neither parent has any traits, we'll always get 0 inherited traits, so we'll always get the "required"
                                            // traits regardless of the roll for `TraitProbabilityDirect`
                                            probabilityGotRequiredFromParent = 1.0f;
                                        }
                                        else if (!desiredParentTraits.Any())
                                        {
                                            // just the chance of getting this number of traits from parents
                                            probabilityGotRequiredFromParent = GameConfig.TraitProbabilityDirect[numInheritedFromParent];
                                        }
                                        else if (numIrrelevantFromParent == 0)
                                        {
                                            // chance of getting exactly the required traits
                                            probabilityGotRequiredFromParent = GameConfig.TraitProbabilityDirect[numInheritedFromParent] / Choose(parentTraits.Count, desiredParentTraits.Count);
                                        }
                                        else
                                        {
                                            // (available traits except desired)
                                            // choose
                                            // (required num irrelevant)
                                            var numCombinationsWithIrrelevantTrait = (float)Choose(parentTraits.Count - desiredParentTraits.Count, numIrrelevantFromParent);

                                            // (all available traits)
                                            // choose
                                            // (actual num inherited from parent)
                                            var numCombinationsWithAnyTraits = (float)Choose(parentTraits.Count, actualNumInheritedFromParent);

                                            // probability of those traits containing the desired traits
                                            // (doesn't affect anything if we don't actually want any of these traits)
                                            // (TODO - is this right? got this simple division from chatgpt)
                                            var probabilityCombinationWithDesiredTraits = desiredParentTraits.Count == 0 ? 1 : (
                                                numCombinationsWithIrrelevantTrait / numCombinationsWithAnyTraits
                                            );

                                            probabilityGotRequiredFromParent = probabilityCombinationWithDesiredTraits * GameConfig.TraitProbabilityDirect[numInheritedFromParent];
                                        }

#if DEBUG
                                        if (probabilityGotRequiredFromParent > 1) Debugger.Break();
#endif

                                        var probabilityGotExactRequiredRandom = GameConfig.TraitRandomAddedProbability[numIrrelevantFromRandom];
                                        probabilityForUpToNumTraits += probabilityGotRequiredFromParent * probabilityGotExactRequiredRandom;
                                    }

                                    if (probabilityForUpToNumTraits <= 0) continue;

#if DEBUG
                                    if (initialProbability == probabilityForUpToNumTraits) Debugger.Break();
#endif

                                    // (not entirely correct, since some irrelevant traits may be specific and inherited by parents. if we know a child
                                    //  may have some specific trait, it may be efficient to breed that child with another parent which also has that
                                    //  irrelevant trait, which would increase the overall likelyhood of a desired trait being inherited)
                                    var potentialIrrelevantTraits = Enumerable
                                        .Range(0, Math.Max(0, numFinalTraits - desiredParentTraits.Count))
                                        .Select(i => new RandomTrait());

                                    possibleResults.Add(new BredPalReference(
                                        db.BreedingByParent[parent1.Pal][parent2.Pal].Child,
                                        preferredParent1,
                                        preferredParent2,
                                        desiredParentTraits.Concat(potentialIrrelevantTraits).ToList(),
                                        probabilityForUpToNumTraits
                                    ));
                                }

                                return possibleResults;
                            })
                            .Where(result => result.BreedingEffort <= MAX_EFFORT)
                            .ToList();

                        return res;
                    })
                    .ToList();

                Console.WriteLine("Filtering {0} potential new instances", newInstances.Count);

                // since we know the breeding effort of each potential instance, we can ignore new instances
                // with higher effort than existing known instances
                //
                // (this is the main optimization that lets this complete in less than a week)
                var bestNewInstances = newInstances
                    .GroupBy(pref => (
                        pref.Pal,
                        pref.Gender,
                        string.Join(" ",
                            pref
                                .Traits
                                .Select(t => t.ToString())
                                .OrderBy(t => t)
                        )
                    ))
                    .Select(g => g.MinBy(pref => pref.BreedingEffort))
                    .ToList();

                Console.WriteLine("Within {0} new instances, reduced to {1} instances by removing duplicates and taking the lowest-effort option", newInstances.Count, bestNewInstances.Count);

                //foreach (var newInst in newInstances)
                var numChanged = 0;
                foreach (var newInst in bestNewInstances)
                {
                    var existingInstances = availablePalInstances.Where(pi =>
                        pi.Pal == newInst.Pal &&
                        pi.Gender == newInst.Gender &&
                        pi.Traits.EqualsTraits(newInst.Traits)
                    ).ToList();

                    var existingInst = existingInstances.SingleOrDefault();

                    if (existingInst != null)
                    {
                        if (newInst.BreedingEffort < existingInst.BreedingEffort)
                        {
                            availablePalInstances.Remove(existingInst);
                            availablePalInstances.Add(newInst);
                            numChanged++;
                        }
                    }
                    else
                    {
                        availablePalInstances.Add(newInst);
                        numChanged++;
                    }
                }

                if (numChanged == 0)
                {
                    Console.WriteLine("Last pass found no new useful options, stopping iteration early");
                    break;
                }
            }

            Console.WriteLine("Took {0}", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds));

            Console.WriteLine("\n\nRESULTS:");
            var matches = availablePalInstances.Where(pref => pref.Pal == targetInstance.Pal && !targetInstance.Traits.Except(pref.Traits).Any()).ToList();
            foreach (var match in matches)
            {
                var tree = new BreedingTree(match);
                tree.Print();
                Console.WriteLine("Should take: {0}\n", match.BreedingEffort);
            }
        }
    }
}
