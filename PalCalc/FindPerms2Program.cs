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
    internal class FindPerms2Program
    {
        // N choose K -> look up column K at row N of pascal's triangle
        class PascalsTriangle
        {
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

            public static PascalsTriangle Instance = new PascalsTriangle();
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
            var MAX_BREEDING_STEPS = 5;

            // max num. irrelevant traits from any parents or children involved in the final breeding steps (including target pal)
            var MAX_IRRELEVANT_TRAITS = 1;

            /* effort in estimated time to get the desired pal w/ traits
             * 
             * - goes by constant breeding time
             * - ignores hatching time
             * - roughly estimates time to catch wild pals with increasing time based on paldex number
            */
            var MAX_EFFORT = TimeSpan.FromHours(10);
            // !!! !!!



            var targetInstance = new PalInstance
            {
                Pal = "Suzaku".ToPal(db),
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
             *   2.2. Filter by whether either parent can reach the target pal without exceeding the max num. breeding steps
             *   2.3. 
             * 
             */

            var palDistances = PalCalcUtils.CalcMinDistances(db);
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

            bool WithinBreedingSteps(Pal pal, int maxSteps) => palDistances[pal][targetInstance.Pal] <= maxSteps;

            HashSet<IPalReference> availablePalInstances = new HashSet<IPalReference>(relevantPals.Where(pi => WithinBreedingSteps(pi.Pal, MAX_BREEDING_STEPS)).Select(i => new OwnedPalReference(i)));
            if (MAX_WILD_PALS > 0)
            {
                foreach (var pal in db.Pals.Where(p => !relevantPals.Any(i => i.Pal == p)).Where(p => WithinBreedingSteps(p, MAX_BREEDING_STEPS)))
                    availablePalInstances.Add(new WildcardPalReference(pal));
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
                var newInstances = availablePalInstances
#if RELEASE
                    .AsParallel()
#endif
                    .SelectMany(parent1 =>
                {
                    var res = availablePalInstances
                        .Where(p2 => parent1 != p2)
                        .Select(i => i.EnsureOppositeGender(db, parent1.Gender))
                        .Where(i => i != null)
                        .Where(i => i.BreedingEffort < MAX_EFFORT)
                        .Select(parent2 =>
                        {
                            if (NumWildPalParticipants(parent1) + NumWildPalParticipants(parent2) > MAX_WILD_PALS) return null;

                            var childPal = db.BreedingByParent[parent1.Pal][parent2.Pal].Child;
                            if (palDistances[childPal][targetInstance.Pal] > MAX_BREEDING_STEPS - s - 1)
                            {
                                return null;
                            }

                            if (NumBredPalParticipants(parent1) + NumBredPalParticipants(parent2) >= MAX_BREEDING_STEPS)
                            {
                                return null;
                            }

                            var parentTraits = parent1.Traits.Concat(parent2.Traits).Distinct().ToList();
                            var desiredParentTraits = targetInstance.Traits.Intersect(parentTraits).ToList();

                            var matchedTraitsProbability = 0.0f;

                            // go through each potential final number of traits, accumulate the probability of any of these exact options
                            // leading to the desired traits within MAX_IRRELEVANT_TRAITS
                            for (int numFinalTraits = 0; numFinalTraits <= GameConfig.MaxTotalTraits; numFinalTraits++)
                            {
                                // only looking for probability of getting all desired parent traits, which means we need at least Count(desired)
                                // total traits
                                if (numFinalTraits < desiredParentTraits.Count) continue;

                                // exceeding Count(desiredTraits) + MAX_IRRELEVANT_TRAITS means we've exceeded the max irrelevant traits allowed
                                if (numFinalTraits > desiredParentTraits.Count + MAX_IRRELEVANT_TRAITS) break;

                                for (int numInheritedFromParent = desiredParentTraits.Count; numInheritedFromParent <= numFinalTraits; numInheritedFromParent++)
                                {
                                    // we may inherit more traits from the parents than the parents actually have (e.g. inherit 4 traits from parents with
                                    // 2 total traits), in which case we'd still inherit just two
                                    //
                                    // this doesn't affect probabilities of getting `numInherited`, but it affects the number of random traits which must
                                    // be added to each `numFinalTraits` and the number of combinations of parent traits that we check
                                    var actualNumInheritedFromParent = Math.Min(numInheritedFromParent, parentTraits.Count);
                                    
                                    var numIrrelevantFromParent = actualNumInheritedFromParent - desiredParentTraits.Count;
                                    var numIrrelevantFromRandom = numFinalTraits - numIrrelevantFromParent;

                                    // can inherit at most 3 random traits; if this `if` is `true` then we've hit a case which would never actually happen
                                    // (e.g. 4 target final traits, 0 from parents, 4 from random)
                                    if (numIrrelevantFromRandom > 3) continue;

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
                                        // (available traits except desired) choose (required num irrelevant)
                                        var numCombinationsWithIrrelevantTrait = (float)Choose(parentTraits.Count - desiredParentTraits.Count, numIrrelevantFromParent);

                                        // (all available traits) choose (actual num inherited from parent)
                                        var numCombinationsWithAnyTraits = (float)Choose(parentTraits.Count, actualNumInheritedFromParent);

                                        // probability of those traits containing the desired traits
                                        // (doesn't affect anything if we don't actually want any of these traits)
                                        // (TODO - is this right? got this simple division from chatgpt)
                                        var probabilityCombinationWithDesiredTraits = desiredParentTraits.Count == 0 ? 1 : (
                                            numCombinationsWithIrrelevantTrait / numCombinationsWithAnyTraits
                                        );

                                        probabilityGotRequiredFromParent = probabilityCombinationWithDesiredTraits * GameConfig.TraitProbabilityDirect[numInheritedFromParent];
                                    }

                                    if (probabilityGotRequiredFromParent > 1) Debugger.Break();

                                    // consider probability of actually getting `numInheritedFromParent` direct-inherited traits

                                    var probabilityGotExactRequiredRandom = GameConfig.TraitRandomAddedProbability[numIrrelevantFromRandom];

                                    matchedTraitsProbability += probabilityGotRequiredFromParent * probabilityGotExactRequiredRandom;
                                }
                            }

                            var result = new BredPalReference(childPal, parent1, parent2, desiredParentTraits, matchedTraitsProbability);

                            return result.BreedingEffort < MAX_EFFORT ? result : null;
                        })
                        .Where(v => v != null)
                        .ToList();

                    return res;
                }).ToList();

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

                foreach (var newInst in newInstances)
                //foreach (var newInst in bestNewInstances)
                {
                    var existingInstances = availablePalInstances.Where(pi =>
                        pi.Pal == newInst.Pal &&
                        pi.Gender == newInst.Gender &&
                        pi.Traits.Count == newInst.Traits.Count &&
                        !pi.Traits.Except(newInst.Traits).Any()
                    ).ToList();

                    var existingInst = existingInstances.SingleOrDefault();

                    if (existingInst != null)
                    {
                        if (newInst.BreedingEffort < existingInst.BreedingEffort)
                        {
                            availablePalInstances.Remove(existingInst);
                            availablePalInstances.Add(newInst);
                        }
                    }
                    else
                    {
                        availablePalInstances.Add(newInst);
                    }
                }
            }

            Console.WriteLine("Took {0}", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds));

            // returns (stepIndex, participant), where higher stepIndex means it's used earlier in the chain
            // (stepIndex=0 means it's used in the current step)
            IEnumerable<(int, IPalReference)> ParticipantsFor(IPalReference pref)
            {
                switch (pref)
                {
                    case BredPalReference bpr:
                        yield return (0, bpr);
                        foreach (var p in ParticipantsFor(bpr.Parent1))
                            yield return (p.Item1 + 1, p.Item2);
                        foreach (var p in ParticipantsFor(bpr.Parent2))
                            yield return (p.Item1 + 1, p.Item2);
                        break;

                    default:
                        yield return (0, pref);
                        break;
                    
                }
            }

            Console.WriteLine("\n\nRESULTS:");
            var matches = availablePalInstances.Where(pref => pref.Pal == targetInstance.Pal && !targetInstance.Traits.Except(pref.Traits).Any()).ToList();
            foreach (var match in matches)
            {
                var participants = ParticipantsFor(match).ToList();
                var numConcurrentSteps = participants.Max(p => p.Item1);

                for (int i = 0; i < numConcurrentSteps; i++)
                {
                    var indentation = new string('\t', i);
                    Console.WriteLine("{0}Phase {1}", indentation, i + 1);

                    var stepIndex = numConcurrentSteps - i;
                    foreach (var part in participants.Where(part => part.Item1 == stepIndex))
                        Console.WriteLine("{0}- Using {1}", indentation, part.Item2);

                    Console.WriteLine("{0}Breed:", indentation);
                    var nextStepIndex = stepIndex - 1;
                    foreach (var result in participants.Where(part => part.Item1 == nextStepIndex && part.Item2 is BredPalReference).Select(part => part.Item2).Cast<BredPalReference>())
                    {
                        Console.WriteLine("{0}- {1}", indentation, result);
                        Console.WriteLine("{0}  from {1} + {2}", indentation, result.Parent1, result.Parent2);
                        Console.WriteLine("{0}  (takes ~{1})", indentation, result.SelfBreedingEffort);
                    }

                    Console.WriteLine();
                }

                Console.WriteLine("Should take: {0}", match.BreedingEffort);
            }
        }
    }
}
