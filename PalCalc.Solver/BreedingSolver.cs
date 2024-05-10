using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public enum SolverPhase
    {
        Initializing,
        Breeding,
        Finished,
    }

    public class SolverStatus
    {
        public SolverPhase CurrentPhase { get; set; }
        public int CurrentStepIndex { get; set; }
        public int TargetSteps { get; set; }
        public bool Canceled { get; set; }
        public int WorkSize { get; set; }
    }

    public class BreedingSolver
    {
        private static ILogger logger = Log.ForContext<BreedingSolver>();

        // returns number of ways you can choose k combinations from a list of n
        // TODO - is this the right way to use pascal's triangle??
        static int Choose(int n, int k) => PascalsTriangle.Instance[n - 1][k - 1];

        GameSettings gameSettings;
        PalDB db;
        List<PalInstance> ownedPals;

        int maxBreedingSteps, maxWildPals, maxBredIrrelevantTraits, maxInputIrrelevantTraits;
        TimeSpan maxEffort;

        /// <param name="db"></param>
        /// <param name="ownedPals"></param>
        /// <param name="maxBreedingSteps"></param>
        /// <param name="maxWildPals"></param>
        /// <param name="maxIrrelevantTraits">
        ///     Max number of irrelevant traits from any parents or children involved in the final breeding steps (including target pal)
        ///     (Lower value runs faster, but considers fewer possibilities)
        /// </param>
        /// <param name="maxEffort">
        ///     Effort in estimated time to get the desired pal with the given traits. Goes by constant breeding time, ignores hatching
        ///     time, and roughly estimates time to catch wild pals (with increasing time based on paldex number).
        /// </param>
        public BreedingSolver(GameSettings gameSettings, PalDB db, List<PalInstance> ownedPals, int maxBreedingSteps, int maxWildPals, int maxInputIrrelevantTraits, int maxBredIrrelevantTraits, TimeSpan maxEffort)
        {
            this.gameSettings = gameSettings;
            this.db = db;
            this.ownedPals = ownedPals;
            this.maxBreedingSteps = maxBreedingSteps;
            this.maxWildPals = maxWildPals;
            this.maxInputIrrelevantTraits = Math.Min(3, maxInputIrrelevantTraits);
            this.maxBredIrrelevantTraits = Math.Min(3, maxBredIrrelevantTraits);
            this.maxEffort = maxEffort;
        }

        public event Action<SolverStatus> SolverStateUpdated;

        // for each available (pal, gender) pair, and for each set of instance traits as a subset of the desired traits (e.g. all male lamballs with "runner",
        // all with "runner and swift", etc.), pick the instance with the fewest total traits
        //
        // (includes pals without any relevant traits, picks the instance with the fewest total traits)
        static List<PalInstance> RelevantInstancesForTraits(PalDB db, List<PalInstance> availableInstances, List<Trait> targetTraits)
        {
            List<PalInstance> relevantInstances = new List<PalInstance>();

            var traitPermutations = targetTraits.Combinations(targetTraits.Count).Select(l => l.ToList()).ToList();
            logger.Debug("Looking for pals with traits:\n- {0}", string.Join("\n- ", traitPermutations.Select(p => $"({string.Join(',', p)})")));

            foreach (var pal in db.Pals)
            {
                foreach (var gender in new List<PalGender>() { PalGender.MALE, PalGender.FEMALE })
                {
                    var instances = availableInstances.Where(i => i.Pal == pal && i.Gender == gender).ToList();
                    var instancesByPermutation = traitPermutations.ToDictionary(p => p, p => new List<PalInstance>());

                    foreach (var instance in instances)
                    {
                        var matchingPermutation = traitPermutations
                            .OrderByDescending(p => p.Count)
                            .ThenBy(p => p.Except(instance.Traits).Count())
                            .First(p => !p.Except(instance.Traits).Any());

                        instancesByPermutation[matchingPermutation].Add(instance);
                    }

                    relevantInstances.AddRange(
                        instancesByPermutation.Values
                            .Where(instances => instances.Count != 0)
                            .Select(instances => instances
                                .OrderBy(i => i.Traits.Count)
                                .ThenBy(i => i.Location.Type switch
                                {
                                    // prefer pals in palbox, then in base, etc
                                    LocationType.Palbox => 0,
                                    LocationType.Base => 1,
                                    LocationType.PlayerParty => 2,
                                    _ => throw new NotImplementedException(),
                                })
                                .First()
                            )
                    );
                }
            }

            return relevantInstances;
        }

        // we have two parents but don't necessarily have definite genders for them, figure out which parent should have which
        // gender (if they're wild/bred pals) for the least overall effort (different pals have different gender probabilities)
        (IPalReference, IPalReference) PreferredParentsGenders(IPalReference parent1, IPalReference parent2)
        {
            IEnumerable<IPalReference> ParentOptions(IPalReference parent)
            {
                if (parent.Gender == PalGender.WILDCARD)
                {
                    yield return parent.WithGuaranteedGender(db, PalGender.MALE);
                    yield return parent.WithGuaranteedGender(db, PalGender.FEMALE);
                }
                else
                {
                    yield return parent;
                }
            }

            var optionsParent1 = ParentOptions(parent1);
            var optionsParent2 = ParentOptions(parent2);

            var parentPairOptions = optionsParent1.SelectMany(p1v => optionsParent2.Where(p2v => p2v.IsCompatibleGender(p1v.Gender)).Select(p2v => (p1v, p2v)));

            Func<IPalReference, IPalReference, TimeSpan> CombinedEffortFunc = gameSettings.MultipleBreedingFarms
                ? ((a, b) => a.BreedingEffort > b.BreedingEffort ? a.BreedingEffort : b.BreedingEffort)
                : ((a, b) => a.BreedingEffort + b.BreedingEffort);

            TimeSpan optimalTime = TimeSpan.Zero;
            bool hasNoPreference = true;
            foreach (var (p1, p2) in parentPairOptions)
            {
                var effort = CombinedEffortFunc(p1, p2);
                if (optimalTime == TimeSpan.Zero) optimalTime = effort;
                else if (optimalTime != effort)
                {
                    hasNoPreference = false;

                    if (effort < optimalTime) optimalTime = effort;
                }
            }

            if (hasNoPreference)
            {
                // either there is no preference or at least 1 parent already has a specific gender
                var p1wildcard = parent1.Gender == PalGender.WILDCARD;
                var p2wildcard = parent2.Gender == PalGender.WILDCARD;

                // should we set a specific gender on p1?
                if (p1wildcard && (
                    !p2wildcard || // p2 is a specific gender
                    parent1.BreedingEffort < parent2.BreedingEffort // p1 takes less effort than p2
                ))
                {
                    return (parent1.WithGuaranteedGender(db, parent2.Gender.OppositeGender()), parent2);
                }

                // should we set a specific gender on p2?
                if (p2wildcard && (
                    !p1wildcard || // p1 is a specific gender
                    parent2.BreedingEffort <= parent1.BreedingEffort // p2 takes less effort than p1 (need <= to resolve cases where self-effort is same for both wildcards)
                ))
                {
                    return (parent1, parent2.WithGuaranteedGender(db, parent1.Gender.OppositeGender()));
                }

                // neither parents are wildcards
                return (parent1, parent2);
            }
            else
            {
                return parentPairOptions.First(p => optimalTime == CombinedEffortFunc(p.p1v, p.p2v));
            }
        }

        /// <summary>
        /// Calculates the probability of a child pal with `numFinalTraits` traits having the all desired traits from
        /// the list of possible parent traits.
        /// </summary>
        /// 
        /// <param name="parentTraits">The the full set of traits from the parents (deduplicated)</param>
        /// <param name="desiredParentTraits">The list of traits you want to be inherited</param>
        /// <param name="numFinalTraits">The exact amount of final traits to calculate for</param>
        /// <returns></returns>
        /// 
        /// <remarks>
        /// e.g. "if we decide the child pal has N traits, what's the probability of containing all of the traits we want"
        /// </remarks>
        /// <remarks>
        /// Should be used repeatedly to calculate probabilities for all possible counts of traits (max 4)
        /// </remarks>
        /// 
        float ProbabilityInheritedTargetTraits(List<Trait> parentTraits, List<Trait> desiredParentTraits, int numFinalTraits)
        {
            // we know we need at least `desiredParentTraits.Count` to be inherited from the parents, but the overall number
            // of traits must be `numFinalTraits`. consider N, N+1, ..., traits inherited from parents, and an inverse amount
            // of randomly-added traits
            //
            // e.g. we want 4 total traits with 2 desired from parents. we could have:
            //
            // - 2 inherited + 2 random
            // - 3 inherited + 1 random
            // - 4 inherited + 0 random
            //
            // ... each of these has a separate probability of getting exactly that outcome.
            //
            // the final probability for these params (fn args) is the sum

            float probabilityForNumTraits = 0.0f;

            for (int numInheritedFromParent = desiredParentTraits.Count; numInheritedFromParent <= numFinalTraits; numInheritedFromParent++)
            {
                // we may inherit more traits from the parents than the parents actually have (e.g. inherit 4 traits from parents with
                // 2 total traits), in which case we'd still inherit just two
                //
                // this doesn't affect probabilities of getting `numInherited`, but it affects the number of random traits which must
                // be added to each `numFinalTraits` and the number of combinations of parent traits that we check
                var actualNumInheritedFromParent = Math.Min(numInheritedFromParent, parentTraits.Count);

                var numIrrelevantFromParent = actualNumInheritedFromParent - desiredParentTraits.Count;
                var numIrrelevantFromRandom = numFinalTraits - actualNumInheritedFromParent;

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
                    probabilityGotRequiredFromParent = GameConstants.TraitProbabilityDirect[numInheritedFromParent];
                }
                else if (numIrrelevantFromParent == 0)
                {
                    // chance of getting exactly the required traits
                    probabilityGotRequiredFromParent = GameConstants.TraitProbabilityDirect[numInheritedFromParent] / Choose(parentTraits.Count, desiredParentTraits.Count);
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

                    probabilityGotRequiredFromParent = probabilityCombinationWithDesiredTraits * GameConstants.TraitProbabilityDirect[numInheritedFromParent];
                }

#if DEBUG
                if (probabilityGotRequiredFromParent > 1) Debugger.Break();
#endif

                var probabilityGotExactRequiredRandom = GameConstants.TraitRandomAddedProbability[numIrrelevantFromRandom];
                probabilityForNumTraits += probabilityGotRequiredFromParent * probabilityGotExactRequiredRandom;
            }

            return probabilityForNumTraits;
        }

        public List<IPalReference> SolveFor(PalSpecifier spec, CancellationToken token)
        {
            if (spec.Traits.Count > GameConstants.MaxTotalTraits)
            {
                throw new Exception("Target trait count cannot exceed max number of traits for a single pal");
            }

            var statusMsg = new SolverStatus() { CurrentPhase = SolverPhase.Initializing, CurrentStepIndex = 0, TargetSteps = maxBreedingSteps, Canceled = token.IsCancellationRequested };
            SolverStateUpdated?.Invoke(statusMsg);

            var relevantPals = RelevantInstancesForTraits(db, ownedPals, spec.Traits)
               .Where(p => p.Traits.Except(spec.Traits).Count() <= maxInputIrrelevantTraits)
               .ToList();

            logger.Debug(
                "Using {relevantCount}/{totalCount} pals as relevant inputs with traits:\n- {summary}",
                relevantPals.Count,
                ownedPals.Count,
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

            bool WithinBreedingSteps(Pal pal, int maxSteps) => db.MinBreedingSteps[pal][spec.Pal] <= maxSteps;

            var initialContent = new List<IPalReference>();
            foreach (
                var palGroup in relevantPals
                    .Where(pi => WithinBreedingSteps(pi.Pal, maxBreedingSteps))
                    .Select(pi => new OwnedPalReference(pi, pi.Traits.ToDedicatedTraits(spec.Traits)))
                    .GroupBy(pi => pi.Pal)
            )
            {
                var pal = palGroup.Key;
                
                // group owned pals by the desired traits they contain, and try to find male+female pairs with the same set of traits + num irrelevant traits
                foreach (
                    var traitGroup in palGroup
                        .GroupBy(p => p.EffectiveTraits
                            // (pad them all to have max number of traits, so the grouping ignores the total number of traits)
                            .Concat(Enumerable.Range(0, GameConstants.MaxTotalTraits - p.EffectiveTraits.Count).Select(_ => new RandomTrait()))
                            .SetHash()
                        )
                        .Select(g => g.ToList())
                )
                {
                    // `traitGroup` is a list of pals which have the same list of desired traits, though they can have varying numbers of undesired traits.
                    // (there should be at most 2, due to the previous processing of `relevantPals` which would restrict this group to, at most, a male + female instance)

                    if (traitGroup.Count > 2) throw new NotImplementedException(); // shouldn't happen

                    if (traitGroup.Select(p => p.EffectiveTraits.Count(t => t is RandomTrait)).Distinct().Count() == 1)
                    {
                        // all pals in this group have the same number of irrelevant traits
                        if (traitGroup.Count == 1)
                        {
                            // only one pal, cant turn into a composite
                            initialContent.Add(traitGroup.Single());
                        }
                        else if (traitGroup.Count == 2)
                        {
                            // two pals with the same desired traits and number of irrelevant traits, treat as a wildcard (composite)
                            initialContent.Add(
                                new CompositeOwnedPalReference(traitGroup[0], traitGroup[1])
                            );
                        }
                    }
                    else
                    {
                        // (can only happen if there are two pals in this group)

                        // male and female have matching desired traits but a different number of irrelevant traits, add them each as individual
                        // options but also allow their combination to be used as a wildcard (traits of the combination will use the "worst-case"
                        // option, i.e. one with no irrelevant and the other with two irrelevant, the composite will have two irrelevant

                        initialContent.Add(traitGroup[0]);
                        initialContent.Add(traitGroup[1]);

                        initialContent.Add(new CompositeOwnedPalReference(traitGroup[0], traitGroup[1]));
                    }
                }
            }

            if (maxWildPals > 0)
            {
                // add wild pals with varying number of random traits
                initialContent.AddRange(
                    db.Pals
                        .Where(p => !relevantPals.Any(i => i.Pal == p))
                        .Where(p => WithinBreedingSteps(p, maxBreedingSteps))
                        .SelectMany(p => Enumerable.Range(0, maxInputIrrelevantTraits).Select(numTraits => new WildPalReference(p, numTraits)))
                        .Where(pi => pi.BreedingEffort <= maxEffort)
                );
            }

            var workingSet = new WorkingSet(initialContent, token);

            for (int s = 0; s < maxBreedingSteps; s++)
            {
                if (token.IsCancellationRequested) break;

                bool didUpdate = workingSet.Process(work =>
                {
                    statusMsg.CurrentPhase = SolverPhase.Breeding;
                    statusMsg.CurrentStepIndex = s;
                    statusMsg.Canceled = token.IsCancellationRequested;
                    statusMsg.WorkSize = work.Count;
                    SolverStateUpdated?.Invoke(statusMsg);

                    logger.Debug("Performing breeding step {step} with {numWork} work items", s+1, work.Count);
                    return work
                        .BatchedForParallel()
                        .AsParallel()
                        .SelectMany(workBatch =>
                            workBatch
                                .TakeWhile(_ => !token.IsCancellationRequested)
                                .Where(p => p.Item1.IsCompatibleGender(p.Item2.Gender))
                                .Where(p => p.Item1.NumWildPalParticipants() + p.Item2.NumWildPalParticipants() <= maxWildPals)
                                .Where(p => p.Item1.NumTotalBreedingSteps + p.Item2.NumTotalBreedingSteps < maxBreedingSteps)
                                .Where(p =>
                                {
                                    var childPal = db.BreedingByParent[p.Item1.Pal][p.Item2.Pal].Child;

                                    return db.MinBreedingSteps[childPal][spec.Pal] <= maxBreedingSteps - s - 1;
                                })
                                .Where(p =>
                                {
                                    // if we disallow any irrelevant traits, neither parents have a useful trait, and at least 1 parent
                                    // has an irrelevant trait, then it's impossible to breed a child with zero total traits
                                    //
                                    // (child would need to have zero since there's nothing useful to inherit and we disallow irrelevant traits,
                                    //  impossible to have zero since a child always inherits at least 1 direct trait if possible)
                                    if (maxBredIrrelevantTraits > 0) return true;

                                    var combinedTraits = p.Item1.EffectiveTraits.Concat(p.Item2.EffectiveTraits);

                                    var anyRelevantFromParents = combinedTraits.Intersect(spec.Traits).Any();
                                    var anyIrrelevantFromParents = combinedTraits.Except(spec.Traits).Any();

                                    return anyRelevantFromParents || !anyIrrelevantFromParents;
                                })
                                .SelectMany(p =>
                                {
                                    var (parent1, parent2) = p;
                                    var (preferredParent1, preferredParent2) = PreferredParentsGenders(parent1, parent2);

                                    var parentTraits = parent1.EffectiveTraits.Concat(parent2.EffectiveTraits).Distinct().ToList();
                                    var desiredParentTraits = spec.Traits.Intersect(parentTraits).ToList();

                                    var possibleResults = new List<IPalReference>();

                                    var probabilityForUpToNumTraits = 0.0f;

                                    // go through each potential final number of traits, accumulate the probability of any of these exact options
                                    // leading to the desired traits within MAX_IRRELEVANT_TRAITS.
                                    //
                                    // we'll generate an option for each possible outcome of up to the max possible number of traits, where each
                                    // option represents the likelyhood of getting all desired traits + up to some number of irrelevant traits
                                    for (int numFinalTraits = desiredParentTraits.Count; numFinalTraits <= Math.Min(GameConstants.MaxTotalTraits, desiredParentTraits.Count + maxBredIrrelevantTraits); numFinalTraits++)
                                    {
#if DEBUG
                                        float initialProbability = probabilityForUpToNumTraits;
#endif

                                        probabilityForUpToNumTraits += ProbabilityInheritedTargetTraits(parentTraits, desiredParentTraits, numFinalTraits);

                                        if (probabilityForUpToNumTraits <= 0) continue;

#if DEBUG
                                        if (initialProbability == probabilityForUpToNumTraits) Debugger.Break();
#endif

                                        // (not entirely correct, since some irrelevant traits may be specific and inherited by parents. if we know a child
                                        //  may have some specific trait, it may be efficient to breed that child with another parent which also has that
                                        //  irrelevant trait, which would increase the overall likelyhood of a desired trait being inherited)
                                        var newTraits = new List<Trait>(numFinalTraits);
                                        newTraits.AddRange(desiredParentTraits);
                                        while (newTraits.Count < numFinalTraits)
                                            newTraits.Add(new RandomTrait());

                                        var res = new BredPalReference(
                                            gameSettings,
                                            db.BreedingByParent[parent1.Pal][parent2.Pal].Child,
                                            preferredParent1,
                                            preferredParent2,
                                            newTraits,
                                            probabilityForUpToNumTraits
                                        );

                                        if (res.BreedingEffort <= maxEffort && workingSet.IsOptimal(res))
                                            possibleResults.Add(res);
                                    }

                                    return possibleResults;
                                })
                                .Where(res => res.BreedingEffort <= maxEffort)
                                .ToList()
                        );
                });

                if (token.IsCancellationRequested) break;

                if (!didUpdate)
                {
                    logger.Debug("Last pass found no new useful options, stopping iteration early");
                    break;
                }
            }

            statusMsg.Canceled = token.IsCancellationRequested;
            statusMsg.CurrentPhase = SolverPhase.Finished;
            SolverStateUpdated?.Invoke(statusMsg);

            return workingSet.Result.Where(pref => pref.Pal == spec.Pal && !spec.Traits.Except(pref.EffectiveTraits).Any()).ToList();
        }
    }
}
