using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

/*
 * TODO
 * 
 * - Add warning when memory size likely exceeds system limits
 */

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
        public bool Paused { get; set; }

        public int CurrentWorkSize { get; set; }

        public int WorkProcessedCount { get; set; }
        public int TotalWorkProcessedCount { get; set; }
    }

    public class SolverStateController
    {
        public CancellationToken CancellationToken { get; set; }
        public bool IsPaused { get; private set; }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
    }

    class WorkBatchProgress
    {
        public int NumProcessed;
    }

    public class BreedingSolver
    {
        private static ILogger logger = Log.ForContext<BreedingSolver>();

        GameSettings gameSettings;
        PalDB db;
        List<PalInstance> ownedPals;

        List<Pal> allowedWildPals;
        List<Pal> bannedBredPals;
        int maxBreedingSteps, maxSolverIterations, maxWildPals, maxBredIrrelevantPassives, maxInputIrrelevantPassives;
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
            int maxSolverIterations,
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
            this.maxSolverIterations = maxSolverIterations;
            this.allowedWildPals = allowedWildPals;
            this.bannedBredPals = bannedBredPals;
            this.maxWildPals = maxWildPals;
            this.maxInputIrrelevantPassives = Math.Min(3, maxInputIrrelevantPassives);
            this.maxBredIrrelevantPassives = Math.Min(3, maxBredIrrelevantPassives);
            this.maxEffort = maxEffort;
            this.maxThreads = maxThreads <= 0 ? Environment.ProcessorCount : Math.Clamp(maxThreads, 1, Environment.ProcessorCount);
        }

        public event Action<SolverStatus> SolverStateUpdated;

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

#if DEBUG && DEBUG_CHECKS
                if (p1wildcard || p2wildcard) Debugger.Break();
#endif

                // neither parents are wildcards
                return (parent1, parent2);
            }
            else
            {
                return parentPairOptions.First(p => optimalTime == CombinedEffortFunc(p.p1v, p.p2v));
            }
        }

        public List<IPalReference> SolveFor(PalSpecifier spec, SolverStateController controller)
        {
            spec.Normalize();

            if (spec.RequiredPassives.Count > GameConstants.MaxTotalPassives)
            {
                throw new Exception("Target passive skill count cannot exceed max number of passive skills for a single pal");
            }

            var statusMsg = new SolverStatus()
            {
                CurrentPhase = SolverPhase.Initializing,
                CurrentStepIndex = 0,
                TargetSteps = maxSolverIterations,
                Canceled = controller.CancellationToken.IsCancellationRequested,
                Paused = controller.IsPaused,
            };
            SolverStateUpdated?.Invoke(statusMsg);

            /* Build the initial list of pals to breed */

            // attempt to deduplicate pals and *intelligently* reduce the initial working set size
            //
            // effectively need to group pals based on their passives, IVs, and gender
            // (though they only count if they're passives/IVs that are desired/in the PalSpecifier)
            //
            // if there are "duplicates", we'll pick based on the pal's IVs and where the pal is stored
            //

            // PalProperty makes it easy to group by different properties
            // (main grouping function)
            var allPropertiesGroupFn = PalProperty.Combine(PalProperty.Pal, PalProperty.RelevantPassives, PalProperty.IvRelevance, PalProperty.Gender);

            // (needed for the last step where we try to combine two pals into one (`CompositePalReference`) if they are
            // different genders but otherwise have all the same properties)
            var allExceptGenderGroupFn = PalProperty.Combine(PalProperty.Pal, PalProperty.RelevantPassives, PalProperty.IvRelevance);

            bool WithinBreedingSteps(Pal pal, int maxSteps) => db.MinBreedingSteps[pal][spec.Pal] <= maxSteps;
            static IV_Range MakeIV(int minValue, int value) =>
                new(
                    isRelevant: minValue != 0 && value >= minValue,
                    value: value
                );

            var initialContent = ownedPals
                // skip pals if they can't be used to reach the desired pals (e.g. Jetragon can only be bred from other Jetragons)
                .Where(p => WithinBreedingSteps(p.Pal, maxBreedingSteps))
                // apply "Max Input Irrelevant Passives" setting
                .Where(p => p.PassiveSkills.Except(spec.DesiredPassives).Count() <= maxInputIrrelevantPassives)
                // convert from Model to Solver repr
                .Select(p => new OwnedPalReference( 
                    instance: p,
                    effectivePassives: p.PassiveSkills.ToDedicatedPassives(spec.DesiredPassives),
                    effectiveIVs: new IV_Set()
                    {
                        HP = MakeIV(spec.IV_HP, p.IV_HP),
                        Attack = MakeIV(spec.IV_Attack, p.IV_Attack),
                        Defense = MakeIV(spec.IV_Defense, p.IV_Defense)
                    }
                ))
                // group pals by their "important" properties and select the "best" pal from each group
                .GroupBy(p => allPropertiesGroupFn(p))
                .Select(g => g
                    .OrderBy(p => p.ActualPassives.Count)
                    .ThenBy(p => PreferredLocationPruning.LocationOrderingOf(p.UnderlyingInstance.Location.Type))
                    .ThenByDescending(p => p.UnderlyingInstance.IV_HP + p.UnderlyingInstance.IV_Attack + p.UnderlyingInstance.IV_Defense)
                    .First()
                )
                // try to consolidate pals which are the same in every way that matters but are opposite genders
                .GroupBy(p => allExceptGenderGroupFn(p))
                .Select(g => g.ToList())
                .SelectMany<List<OwnedPalReference>, IPalReference>(g =>
                {
                    // only one pal in this group, cant turn into a composite, keep as-is
                    if (g.Count == 1) return g;

                    // shouldn't happen, at this point groups should have at most one male and at most one female
                    if (g.Count != 2) throw new NotImplementedException();

                    var malePal = g.SingleOrDefault(p => p.Gender == PalGender.MALE);
                    var femalePal = g.SingleOrDefault(p => p.Gender == PalGender.FEMALE);
                    var composite = new CompositeOwnedPalReference(malePal, femalePal);

                    // the pals are practically the same aside from gender, i.e. they satisfy all the same requirements, but they could
                    // still have different numbers of irrelevant passives.
                    //
                    // if they're *really* the same in all aspects, we can just merge them, otherwise we can merge but
                    // should still keep track of the original pals

                    // (note - these pals weren't combined in earlier groupings since PalProperty.RelevantPassives is intentionally used
                    //         instead of EffectivePassives or ActualPassives)
                    if (malePal.EffectivePassivesHash == femalePal.EffectivePassivesHash)
                    {
                        return [composite];
                    }
                    else
                    {
                        return [
                            malePal,
                            femalePal,
                            composite
                        ];
                    }
                })
                .ToList();

            if (maxWildPals > 0)
            {
                // add wild pals with varying number of random passives
                initialContent.AddRange(
                    allowedWildPals
                        .Where(p => !ownedPals.Any(i => i.Pal == p))
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

            var workingSet = new WorkingSet(spec, pruningBuilder, initialContent, maxThreads, controller.CancellationToken);

            for (int s = 0; s < maxSolverIterations; s++)
            {
                if (controller.CancellationToken.IsCancellationRequested) break;

                var wotByPalId = db.PalsById.Keys.ToFrozenDictionary(id => id, _ => new ConcurrentDictionary<int, TimeSpan>());
                List<WorkBatchProgress> progressEntries = [];

                bool didUpdate = workingSet.Process(work =>
                {
                    logger.Debug("Performing breeding step {step} with {numWork} work items", s+1, work.Count);

                    statusMsg.CurrentPhase = SolverPhase.Breeding;
                    statusMsg.CurrentStepIndex = s;
                    statusMsg.Canceled = controller.CancellationToken.IsCancellationRequested;
                    statusMsg.CurrentWorkSize = work.Count;
                    statusMsg.WorkProcessedCount = 0;
                    SolverStateUpdated?.Invoke(statusMsg);

                    void EmitProgressMsg(object _)
                    {
                        lock (progressEntries)
                        {
                            var progress = progressEntries.Sum(e => e.NumProcessed);
                            statusMsg.WorkProcessedCount = progress;
                        }
                        statusMsg.Paused = controller.IsPaused;
                        SolverStateUpdated?.Invoke(statusMsg);
                    }

                    const int updateInterval = 100;
                    var progressTimer = new Timer(EmitProgressMsg, null, updateInterval, updateInterval);

                    var resEnum = work
                        .BatchedForParallel()
                        .AsParallel()
                        .WithDegreeOfParallelism(maxThreads)
                        .SelectMany(workBatch =>
                        {
                            var progress = new WorkBatchProgress();
                            lock (progressEntries)
                                progressEntries.Add(progress);

                            return workBatch
                                .Tap(_ =>
                                {
                                    while (controller.IsPaused)
                                        Thread.Sleep(1);
                                })
                                .Tap(_ => progress.NumProcessed++)
                                .TakeWhile(_ => !controller.CancellationToken.IsCancellationRequested)
                                .Where(p => p.Item1.IsCompatibleGender(p.Item2.Gender))
                                .Where(p => p.Item1.NumWildPalParticipants() + p.Item2.NumWildPalParticipants() <= maxWildPals)
                                .Where(p => p.Item1.NumTotalBreedingSteps + p.Item2.NumTotalBreedingSteps < maxBreedingSteps)
                                .Where(p =>
                                {
                                    var childPals = db.BreedingByParent[p.Item1.Pal][p.Item2.Pal].Select(br => br.Child);

                                    // don't bother checking any pals if it's impossible for them to reach the target within the remaining
                                    // number of iterations
                                    return childPals.Any(c => db.MinBreedingSteps[c][spec.Pal] <= maxSolverIterations - s - 1);
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

                                    return (
                                        // any relevant from parents?
                                        combinedPassives.Intersect(spec.DesiredPassives).Any() ||
                                        // no irrelevant passives from parents?
                                        !combinedPassives.Except(spec.DesiredPassives).Any()
                                    );
                                })
                                .SelectMany(p =>
                                {
                                    var (parent1, parent2) = p;

                                    // if both parents are wildcards, go through the list of possible gender-specific breeding results
                                    // and modify the parent genders to cover each possible child

#if DEBUG && DEBUG_CHECKS
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

#if DEBUG && DEBUG_CHECKS
                                    if (
                                        // if either parent is a wildcard
                                        (parent1.Gender == PalGender.WILDCARD || parent2.Gender == PalGender.WILDCARD) &&
                                        // the other parent must be an opposite-wildcard
                                        (parent1.Gender != PalGender.OPPOSITE_WILDCARD && parent2.Gender != PalGender.OPPOSITE_WILDCARD)
                                    ) Debugger.Break();
#endif

                                    // Note: We need to use `ActualPassives` for inheritance calc, NOT `EffectivePassives`. If we have:
                                    //
                                    //    Parent 1: [A, B, D]
                                    //    Parent 2: [A, B]
                                    //    Combined + Deduped: [A, B, D]
                                    //
                                    // (Where D is desired, A and B are irrelevant)
                                    //
                                    // Their effective passives become:
                                    //
                                    //    Parent 1: [Random 1, Random 2, D]
                                    //    Parent 2: [Random 3, Random 4]
                                    //    Combined + Deduped: [Random 1, Random 2, Random 3, Random 4, D]
                                    //
                                    // The list of deduplicated passives changes.
                                    //
                                    // Desired passive chance goes up with a smaller list of combined + deduped passives,
                                    // so we'd end up overestimating the effort of parents which have the same (but irrelevant)
                                    // passives.

                                    var parentPassives = parent1.ActualPassives.Concat(parent2.ActualPassives).Distinct().ToList();
                                    var possibleResults = new List<IPalReference>();

                                    var passiveSkillPerms = PassiveSkillPermutations(
                                        spec.RequiredPassives.Intersect(parentPassives).ToList(),
                                        spec.OptionalPassives.Intersect(parentPassives).ToList()
                                    ).Select(p => p.ToList()).ToList();

                                    var ivsProbability = Probabilities.IVs.ProbabilityInheritedTargetIVs(parent1.IVs, parent2.IVs);

                                    IV_IValue MergeIVs(IV_IValue a, IV_IValue b) =>
                                        (a, b) switch
                                        {
                                            (IV_IValue, IV_IValue) when a.IsRelevant && !b.IsRelevant => a,
                                            (IV_IValue, IV_IValue) when !a.IsRelevant && b.IsRelevant => b,

                                            (IV_IValue, IV_Random) => a,
                                            (IV_Random, IV_IValue) => b,

                                            (IV_Range va, IV_Range vb) => IV_Range.Merge(va, vb),
                                            _ => throw new NotImplementedException()
                                        };

                                    var finalIVs = new IV_Set()
                                    {
                                        HP = MergeIVs(parent1.IVs.HP, parent2.IVs.HP),
                                        Attack = MergeIVs(parent1.IVs.Attack, parent2.IVs.Attack),
                                        Defense = MergeIVs(parent1.IVs.Defense, parent2.IVs.Defense)
                                    };

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

                                            probabilityForUpToNumPassives += Probabilities.Passives.ProbabilityInheritedTargetPassives(parentPassives, targetPassives, numFinalPassives);

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
                                                probabilityForUpToNumPassives,
                                                finalIVs,
                                                ivsProbability
                                            );

                                            var workingOptimalTimes = wotByPalId[res.Pal.Id];

                                            if (!bannedBredPals.Contains(res.Pal))
                                            {
                                                var effort = res.BreedingEffort;
                                                if (effort <= maxEffort && (spec.IsSatisfiedBy(res) || workingSet.IsOptimal(res)))
                                                {
                                                    var resultId = WorkingSet.DefaultGroupFn(res);

                                                    bool updated = workingOptimalTimes.TryAdd(resultId, effort);
                                                    while (!updated)
                                                    {
                                                        var v = workingOptimalTimes[resultId];
                                                        if (v < effort) break;

                                                        updated = workingOptimalTimes.TryUpdate(resultId, effort, v);
                                                    }

                                                    if (updated)
                                                    {
                                                        possibleResults.Add(res);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    return possibleResults;
                                })
                                .Where(res => res.BreedingEffort <= maxEffort)
                                .ToList();
                        });

                    var res = resEnum.ToList();
                    progressTimer.Dispose();

                    return res;
                });

                if (controller.CancellationToken.IsCancellationRequested) break;

                lock(progressEntries)
                    statusMsg.WorkProcessedCount = progressEntries.Sum(e => e.NumProcessed);

                statusMsg.TotalWorkProcessedCount += statusMsg.WorkProcessedCount;

                if (!didUpdate)
                {
                    logger.Debug("Last pass found no new useful options, stopping iteration early");
                    break;
                }
            }

            statusMsg.Canceled = controller.CancellationToken.IsCancellationRequested;
            statusMsg.CurrentPhase = SolverPhase.Finished;
            SolverStateUpdated?.Invoke(statusMsg);

            return workingSet.Result.Select(r =>
            {
                if (spec.RequiredGender != PalGender.WILDCARD)
                    return r.WithGuaranteedGender(db, spec.RequiredGender);
                else
                    return r;

            }).ToList();
        }
    }
}
