using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
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

        List<Pal> allowedWildPals;
        List<Pal> bannedBredPals;
        int maxBreedingSteps, maxWildPals, maxBredIrrelevantPassives, maxInputIrrelevantPassives;
        TimeSpan maxEffort;
        PruningRulesBuilder pruningBuilder;
        int maxThreads;

        /// <param name="db"></param>
        /// <param name="ownedPals"></param>
        /// <param name="maxBreedingSteps"></param>
        /// <param name="maxWildPals"></param>
        /// <param name="maxIrrelevantPassives">
        ///     Max number of irrelevant passive skills from any parents or children involved in the final breeding steps (including target pal)
        ///     (Lower value runs faster, but considers fewer possibilities)
        /// </param>
        /// <param name="maxEffort">
        ///     Effort in estimated time to get the desired pal with the given passive skills. Goes by constant breeding time, ignores hatching
        ///     time, and roughly estimates time to catch wild pals (with increasing time based on paldex number).
        /// </param>
        public BreedingSolver(
            GameSettings gameSettings,
            PalDB db,
            PruningRulesBuilder pruningBuilder,
            List<PalInstance> ownedPals,
            int maxBreedingSteps,
            int maxWildPals,
            List<Pal> allowedWildPals,
            List<Pal> bannedBredPals,
            int maxInputIrrelevantPassives,
            int maxBredIrrelevantPassives,
            TimeSpan maxEffort,
            int maxThreads
        )
        {
            this.gameSettings = gameSettings;
            this.db = db;
            this.pruningBuilder = pruningBuilder;
            this.ownedPals = ownedPals;
            this.maxBreedingSteps = maxBreedingSteps;
            this.allowedWildPals = allowedWildPals;
            this.bannedBredPals = bannedBredPals;
            this.maxWildPals = maxWildPals;
            this.maxInputIrrelevantPassives = Math.Min(3, maxInputIrrelevantPassives);
            this.maxBredIrrelevantPassives = Math.Min(3, maxBredIrrelevantPassives);
            this.maxEffort = maxEffort;
            this.maxThreads = maxThreads <= 0 ? Environment.ProcessorCount : Math.Clamp(maxThreads, 1, Environment.ProcessorCount);
        }

        public event Action<SolverStatus> SolverStateUpdated;

        // for each available (pal, gender) pair, and for each set of instance passives as a subset of the desired passives (e.g. all male lamballs with "runner",
        // all with "runner and swift", etc.), pick the instance with the fewest total passives
        //
        // (includes pals without any relevant passives, picks the instance with the fewest total passives)
        static List<PalInstance> RelevantInstancesForPassiveSkills(PalDB db, List<PalInstance> availableInstances, List<PassiveSkill> targetPassives)
        {
            List<PalInstance> relevantInstances = new List<PalInstance>();

            var passivesPermutations = targetPassives.Combinations(targetPassives.Count).Select(l => l.ToList()).ToList();
            logger.Debug("Looking for pals with passives:\n- {0}", string.Join("\n- ", passivesPermutations.Select(p => $"({string.Join(',', p)})")));

            foreach (var pal in db.Pals)
            {
                foreach (var gender in new List<PalGender>() { PalGender.MALE, PalGender.FEMALE })
                {
                    var instances = availableInstances.Where(i => i.Pal == pal && i.Gender == gender).ToList();
                    var instancesByPermutation = passivesPermutations.ToDictionary(p => p, p => new List<PalInstance>());

                    foreach (var instance in instances)
                    {
                        var matchingPermutation = passivesPermutations
                            .OrderByDescending(p => p.Count)
                            .ThenBy(p => p.Except(instance.PassiveSkills).Count())
                            .First(p => !p.Except(instance.PassiveSkills).Any());

                        instancesByPermutation[matchingPermutation].Add(instance);
                    }

                    relevantInstances.AddRange(
                        instancesByPermutation.Values
                            .Where(instances => instances.Count != 0)
                            .Select(instances => instances
                                .OrderBy(i => i.PassiveSkills.Count)
                                .ThenBy(i => PreferredLocationPruning.LocationOrderingOf(i.Location.Type))
                                .ThenByDescending(i => i.IV_HP + i.IV_Shot + i.IV_Defense)
                                .First()
                            )
                    );
                }
            }

            return relevantInstances;
        }

        /// <summary>
        /// Creates a list of desired combinations of passives. Meant to handle the case where there are over MAX_PASSIVES desired passives.
        /// The `requiredPassives` should never have more than MAX_PASSIVES due to a check in `SolveFor`, so this logic is only really
        /// necessary if `requiredPassives` + `optionalPassives` brings us over MAX_PASSIVES.
        /// </summary>
        /// <param name="requiredPassives">The list of passives that will be contained in all permutations.</param>
        /// <param name="optionalPassives">The list of passives that will appear at least once across the permutations, if possible.</param>
        /// <returns></returns>
        static IEnumerable<IEnumerable<PassiveSkill>> PassiveSkillPermutations(IEnumerable<PassiveSkill> requiredPassives, IEnumerable<PassiveSkill> optionalPassives)
        {
#if DEBUG && DEBUG_CHECKS
            if (
                requiredPassives.Count() > GameConstants.MaxTotalPassives ||
                requiredPassives.Distinct().Count() != requiredPassives.Count() ||
                optionalPassives.Distinct().Count() != optionalPassives.Count() ||
                requiredPassives.Intersect(optionalPassives).Any()
            ) Debugger.Break();
#endif

            // can't add any optional passives, just return required passives
            if (!optionalPassives.Any() || requiredPassives.Count() == GameConstants.MaxTotalPassives)
            {
                yield return requiredPassives;
                yield break;
            }

            var numTotalPassives = requiredPassives.Count() + optionalPassives.Count();
            // we're within the passive limit, return all passives
            if (numTotalPassives <= GameConstants.MaxTotalPassives)
            {
                yield return requiredPassives.Concat(optionalPassives);
                yield break;
            }

            var maxOptionalPassives = GameConstants.MaxTotalPassives - requiredPassives.Count();
            foreach (var optional in optionalPassives.ToList().Combinations(maxOptionalPassives).Where(c => c.Any()))
            {
                var res = requiredPassives.Concat(optional);
                yield return res;
            }
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
        /// Calculates the probability of a child pal with `numFinalPassives` passive skills having the all desired passives from
        /// the list of possible parent passives.
        /// </summary>
        /// 
        /// <param name="parentPassives">The the full set of passive skills from the parents (deduplicated)</param>
        /// <param name="desiredParentPassives">The list of passive skills you want to be inherited</param>
        /// <param name="numFinalPassives">The exact amount of final passive skills to calculate for</param>
        /// <returns></returns>
        /// 
        /// <remarks>
        /// e.g. "if we decide the child pal has N passive skills, what's the probability of containing all of the passives we want"
        /// </remarks>
        /// <remarks>
        /// Should be used repeatedly to calculate probabilities for all possible counts of passive skills (max 4)
        /// </remarks>
        /// 
        float ProbabilityInheritedTargetPassives(List<PassiveSkill> parentPassives, List<PassiveSkill> desiredParentPassives, int numFinalPassives)
        {
            // we know we need at least `desiredParentPassives.Count` to be inherited from the parents, but the overall number
            // of passives must be `numFinalPassives`. consider N, N+1, ..., passives inherited from parents, and an inverse amount
            // of randomly-added passives
            //
            // e.g. we want 4 total passives with 2 desired from parents. we could have:
            //
            // - 2 inherited + 2 random
            // - 3 inherited + 1 random
            // - 4 inherited + 0 random
            //
            // ... each of these has a separate probability of getting exactly that outcome.
            //
            // the final probability for these params (fn args) is the sum

            float probabilityForNumPassives = 0.0f;

            for (int numInheritedFromParent = desiredParentPassives.Count; numInheritedFromParent <= numFinalPassives; numInheritedFromParent++)
            {
                // we may inherit more passives from the parents than the parents actually have (e.g. inherit 4 passives from parents with
                // 2 total passives), in which case we'd still inherit just two
                //
                // this doesn't affect probabilities of getting `numInherited`, but it affects the number of random passives which must
                // be added to each `numFinalPassives` and the number of combinations of parent passives that we check
                var actualNumInheritedFromParent = Math.Min(numInheritedFromParent, parentPassives.Count);

                var numIrrelevantFromParent = actualNumInheritedFromParent - desiredParentPassives.Count;
                var numIrrelevantFromRandom = numFinalPassives - actualNumInheritedFromParent;

#if DEBUG && DEBUG_CHECKS
                if (numIrrelevantFromRandom < 0) Debugger.Break();
#endif

                float probabilityGotRequiredFromParent;
                if (numInheritedFromParent == 0)
                {
                    // would only happen if neither parent has a desired passive

                    // the only way we could get zero inherited passives is if neither parent actually has any passives, otherwise
                    // it (seems to) be impossible to get zero direct inherited passives (unconfirmed from reddit thread)
                    if (parentPassives.Count > 0) continue;

                    // if neither parent has any passives, we'll always get 0 inherited passives, so we'll always get the "required"
                    // passives regardless of the roll for `PassiveProbabilityDirect`
                    probabilityGotRequiredFromParent = 1.0f;
                }
                else if (!desiredParentPassives.Any())
                {
                    // just the chance of getting this number of passives from parents
                    probabilityGotRequiredFromParent = GameConstants.PassiveProbabilityDirect[numInheritedFromParent];
                }
                else if (numIrrelevantFromParent == 0)
                {
                    // chance of getting exactly the required passives
                    probabilityGotRequiredFromParent = GameConstants.PassiveProbabilityDirect[numInheritedFromParent] / Choose(parentPassives.Count, desiredParentPassives.Count);
                }
                else
                {
                    // (available passives except desired)
                    // choose
                    // (required num irrelevant)
                    var numCombinationsWithIrrelevantPassive = (float)Choose(parentPassives.Count - desiredParentPassives.Count, numIrrelevantFromParent);

                    // (all available passives)
                    // choose
                    // (actual num inherited from parent)
                    var numCombinationsWithAnyPassives = (float)Choose(parentPassives.Count, actualNumInheritedFromParent);

                    // probability of those passives containing the desired passives
                    // (doesn't affect anything if we don't actually want any of these passives)
                    // (TODO - is this right? got this simple division from chatgpt)
                    var probabilityCombinationWithDesiredPassives = desiredParentPassives.Count == 0 ? 1 : (
                        numCombinationsWithIrrelevantPassive / numCombinationsWithAnyPassives
                    );

                    probabilityGotRequiredFromParent = probabilityCombinationWithDesiredPassives * GameConstants.PassiveProbabilityDirect[numInheritedFromParent];
                }

#if DEBUG && DEBUG_CHECKS
                if (probabilityGotRequiredFromParent > 1) Debugger.Break();
#endif

                var probabilityGotExactRequiredRandom = GameConstants.PassiveRandomAddedProbability[numIrrelevantFromRandom];
                probabilityForNumPassives += probabilityGotRequiredFromParent * probabilityGotExactRequiredRandom;
            }

            return probabilityForNumPassives;
        }

        public List<IPalReference> SolveFor(PalSpecifier spec, CancellationToken token)
        {
            spec.Normalize();

            if (spec.RequiredPassives.Count > GameConstants.MaxTotalPassives)
            {
                throw new Exception("Target passive skill count cannot exceed max number of passive skills for a single pal");
            }

            var statusMsg = new SolverStatus() { CurrentPhase = SolverPhase.Initializing, CurrentStepIndex = 0, TargetSteps = maxBreedingSteps, Canceled = token.IsCancellationRequested };
            SolverStateUpdated?.Invoke(statusMsg);

            var relevantPals = RelevantInstancesForPassiveSkills(db, ownedPals, spec.DesiredPassives.ToList())
               .Where(p => p.PassiveSkills.Except(spec.DesiredPassives).Count() <= maxInputIrrelevantPassives)
               .ToList();

            logger.Debug(
                "Using {relevantCount}/{totalCount} pals as relevant inputs with passive skills:\n- {summary}",
                relevantPals.Count,
                ownedPals.Count,
                string.Join("\n- ",
                    relevantPals
                        .OrderBy(p => p.Pal.Name)
                        .ThenBy(p => p.Gender)
                        .ThenBy(p => string.Join(" ", p.PassiveSkills.OrderBy(t => t.Name)))
                )
            );

            // `relevantPals` is now a list of all captured Pal types, where multiple of the same pal
            // may be included if they have different genders and/or different matching subsets of
            // the desired passives

            bool WithinBreedingSteps(Pal pal, int maxSteps) => db.MinBreedingSteps[pal][spec.Pal] <= maxSteps;

            var initialContent = new List<IPalReference>();
            foreach (
                var palGroup in relevantPals
                    .Where(pi => WithinBreedingSteps(pi.Pal, maxBreedingSteps))
                    .Select(pi => new OwnedPalReference(pi, pi.PassiveSkills.ToDedicatedPassives(spec.DesiredPassives)))
                    .GroupBy(pi => pi.Pal)
            )
            {
                var pal = palGroup.Key;

                // group owned pals by the desired passives they contain, and try to find male+female pairs with the same set of passives + num irrelevant passives
                foreach (
                    var passiveGroup in palGroup
                        .GroupBy(p => p.EffectivePassives
                            // (pad them all to have max number of passives, so the grouping ignores the total number of passives)
                            .Concat(Enumerable.Range(0, GameConstants.MaxTotalPassives - p.EffectivePassives.Count).Select(_ => new RandomPassiveSkill()))
                            .SetHash()
                        )
                        .Select(g => g.ToList())
                )
                {
                    // `passiveGroup` is a list of pals which have the same list of desired passives, though they can have varying numbers of undesired passives.
                    // (there should be at most 2, due to the previous processing of `relevantPals` which would restrict this group to, at most, a male + female instance)

                    if (passiveGroup.Count > 2) throw new NotImplementedException(); // shouldn't happen

                    if (passiveGroup.Select(p => p.EffectivePassives.Count(t => t is RandomPassiveSkill)).Distinct().Count() == 1)
                    {
                        // all pals in this group have the same number of irrelevant passives
                        if (passiveGroup.Count == 1)
                        {
                            // only one pal, cant turn into a composite
                            initialContent.Add(passiveGroup.Single());
                        }
                        else if (passiveGroup.Count == 2)
                        {
                            // two pals with the same desired passives and number of irrelevant passives, treat as a wildcard (composite)
                            initialContent.Add(
                                new CompositeOwnedPalReference(passiveGroup[0], passiveGroup[1])
                            );
                        }
                    }
                    else
                    {
                        // (can only happen if there are two pals in this group)

                        // male and female have matching desired passives but a different number of irrelevant passives, add them each as individual
                        // options but also allow their combination to be used as a wildcard (passives of the combination will use the "worst-case"
                        // option, i.e. one with no irrelevant and the other with two irrelevant, the composite will have two irrelevant

                        initialContent.Add(passiveGroup[0]);
                        initialContent.Add(passiveGroup[1]);

                        initialContent.Add(new CompositeOwnedPalReference(passiveGroup[0], passiveGroup[1]));
                    }
                }
            }

            if (maxWildPals > 0)
            {
                // add wild pals with varying number of random passives
                initialContent.AddRange(
                    allowedWildPals
                        .Where(p => !relevantPals.Any(i => i.Pal == p))
                        .Where(p => WithinBreedingSteps(p, maxBreedingSteps))
                        .SelectMany(p =>
                            Enumerable
                                .Range(
                                    0,
                                    // number of "effectively random" passives should exclude guaranteed passives which are part of the desired list of passives
                                    Math.Max(
                                        0,
                                        maxInputIrrelevantPassives - p.GuaranteedPassiveSkills(db).Except(spec.DesiredPassives).Count()
                                    )
                                )
                                .Select(numRandomPassives => new WildPalReference(p, p.GuaranteedPassiveSkills(db).Intersect(spec.DesiredPassives), numRandomPassives))
                        )
                        .Where(pi => pi.BreedingEffort <= maxEffort)
                );
            }

            var workingSet = new WorkingSet(spec, pruningBuilder, initialContent, maxThreads, token);

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
                        .Batched(work.Count / maxThreads + 1)
                        .AsParallel()
                        .WithDegreeOfParallelism(maxThreads)
                        .SelectMany(workBatch =>
                            workBatch
                                .TakeWhile(_ => !token.IsCancellationRequested)
                                .Where(p => p.Item1.IsCompatibleGender(p.Item2.Gender))
                                .Where(p => p.Item1.NumWildPalParticipants() + p.Item2.NumWildPalParticipants() <= maxWildPals)
                                .Where(p => p.Item1.NumTotalBreedingSteps + p.Item2.NumTotalBreedingSteps < maxBreedingSteps)
                                .Where(p =>
                                {
                                    var childPals = db.BreedingByParent[p.Item1.Pal][p.Item2.Pal].Select(br => br.Child);

                                    return childPals.Any(c => db.MinBreedingSteps[c][spec.Pal] <= maxBreedingSteps - s - 1);
                                })
                                .Where(p =>
                                {
                                    // if we disallow any irrelevant passives, neither parents have a useful passive, and at least 1 parent
                                    // has an irrelevant passive, then it's impossible to breed a child with zero total passives
                                    //
                                    // (child would need to have zero since there's nothing useful to inherit and we disallow irrelevant passives,
                                    //  impossible to have zero since a child always inherits at least 1 direct passive if possible)
                                    if (maxBredIrrelevantPassives > 0) return true;

                                    var combinedPassives = p.Item1.EffectivePassives.Concat(p.Item2.EffectivePassives);

                                    var anyRelevantFromParents = combinedPassives.Intersect(spec.DesiredPassives).Any();
                                    var anyIrrelevantFromParents = combinedPassives.Except(spec.DesiredPassives).Any();

                                    return anyRelevantFromParents || !anyIrrelevantFromParents;
                                })
                                .SelectMany(p =>
                                {
                                    var (parent1, parent2) = p;

                                    // if both parents are wildcards, go through the list of possible gender-specific breeding results
                                    // and modify the parent genders to cover each possible child

#if DEBUG
                                    // (shouldn't happen)

                                    if (parent1.Gender == PalGender.OPPOSITE_WILDCARD || parent2.Gender == PalGender.OPPOSITE_WILDCARD)
                                        Debugger.Break();
#endif

                                    IEnumerable<(IPalReference, IPalReference)> ExpandGendersByChildren()
                                    {
                                        if (parent1.Gender != PalGender.WILDCARD || parent2.Gender != PalGender.WILDCARD)
                                        {
                                            yield return p;
                                        }
                                        else
                                        {
                                            var withModifiedGenders = db.BreedingByParent[parent1.Pal][parent2.Pal].Select(br =>
                                            {
                                                return (
                                                    parent1.WithGuaranteedGender(db, br.RequiredGenderOf(parent1.Pal)),
                                                    parent2.WithGuaranteedGender(db, br.RequiredGenderOf(parent2.Pal))
                                                );
                                            });

                                            foreach (var res in withModifiedGenders)
                                                yield return res;
                                        }
                                    }

                                    return ExpandGendersByChildren();
                                })
                                .Select(p =>
                                {
                                    // arbitrary reordering of (p1, p2) to prevent duplicate results from swapped pairs
                                    // (though this shouldn't be necessary if the `IResultPruning` impls are working right?)
                                    var (parent1, parent2) = p;
                                    if (parent1.GetHashCode() < parent2.GetHashCode()) return (parent1, parent2);
                                    else return (parent2, parent1);
                                })
                                .Select(p => PreferredParentsGenders(p.Item1, p.Item2))
                                .SelectMany(p =>
                                {
                                    var (parent1, parent2) = p;

                                    var parentPassives = parent1.EffectivePassives.Concat(parent2.EffectivePassives).Distinct().ToList();
                                    var possibleResults = new List<IPalReference>();

                                    var passiveSkillPerms = PassiveSkillPermutations(
                                        spec.RequiredPassives.Intersect(parentPassives).ToList(),
                                        spec.OptionalPassives.Intersect(parentPassives).ToList()
                                    ).Select(p => p.ToList()).ToList();

                                    foreach (var targetPassives in passiveSkillPerms)
                                    {
                                        // go through each potential final number of passives, accumulate the probability of any of these exact options
                                        // leading to the desired passives within MAX_IRRELEVANT_PASSIVES.
                                        //
                                        // we'll generate an option for each possible outcome of up to the max possible number of passives, where each
                                        // option represents the likelyhood of getting all desired passives + up to some number of irrelevant passives
                                        var probabilityForUpToNumPassives = 0.0f;

                                        for (int numFinalPassives = targetPassives.Count; numFinalPassives <= Math.Min(GameConstants.MaxTotalPassives, targetPassives.Count + maxBredIrrelevantPassives); numFinalPassives++)
                                        {
#if DEBUG && DEBUG_CHECKS
                                            float initialProbability = probabilityForUpToNumPassives;
#endif

                                            probabilityForUpToNumPassives += ProbabilityInheritedTargetPassives(parentPassives, targetPassives, numFinalPassives);

                                            if (probabilityForUpToNumPassives <= 0)
                                                continue;

#if DEBUG && DEBUG_CHECKS
                                            if (initialProbability == probabilityForUpToNumPassives) Debugger.Break();
#endif

                                            // (not entirely correct, since some irrelevant passives may be specific and inherited by parents. if we know a child
                                            //  may have some specific passive, it may be efficient to breed that child with another parent which also has that
                                            //  irrelevant passive, which would increase the overall likelyhood of a desired passive being inherited)
                                            var newPassives = new List<PassiveSkill>(numFinalPassives);
                                            newPassives.AddRange(targetPassives);
                                            while (newPassives.Count < numFinalPassives)
                                                newPassives.Add(new RandomPassiveSkill());

                                            var res = new BredPalReference(
                                                gameSettings,
                                                db.BreedingByParent[parent1.Pal][parent2.Pal]
                                                    .Single(br => br.Matches(parent1.Pal, parent1.Gender, parent2.Pal, parent2.Gender))
                                                    .Child,
                                                parent1,
                                                parent2,
                                                newPassives,
                                                probabilityForUpToNumPassives
                                            );

                                            if (!bannedBredPals.Contains(res.Pal))
                                            {
                                                if (res.BreedingEffort <= maxEffort && (spec.IsSatisfiedBy(res) || workingSet.IsOptimal(res)))
                                                    possibleResults.Add(res);
                                            }
                                        }
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

            return workingSet.Result.ToList();
        }
    }
}
