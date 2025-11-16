using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// note:
// A lot of this logic was originally written using LINQ. This was convenient and readable, but
// there are a lot of hot-paths in this file, and the excessive LINQ iterator + enumerator instances
// were causing lots of GC activity which added overhead and limited concurrency.
//
// Similarly, there are a number of places where an enumerator (i.e. `yield return`) would be easier
// to understand + debug, but these have the same GC overhead issues.
//
// An object pool is being used for any types which were found to have excessive allocations
// while profiling.

namespace PalCalc.Solver
{
    /// <summary>
    /// Stores the work progress of a single BreedingBatchSolver.
    /// </summary>
    internal class WorkBatchProgress
    {
        public long NumProcessed;
    }

    /// <summary>
    /// Represents the shared state of a single solver iteration.
    /// </summary>
    /// <param name="StepIndex">The current step num. being processed</param>
    /// <param name="Spec">The target pal being solved for</param>
    /// <param name="WorkingSet">The current set of finalized optimal pals</param>
    /// <param name="WorkingOptimalTimesByPalId">
    /// A rough map of pals and their properties, to the fastest time seen for that pair during this step.
    /// 
    /// While the `WorkingSet` has the final say in which "optimal" pals are kept, this acts as an extra "soft"
    /// set of optimal times to reduce the number results returned to the working set for processing.
    /// </param>
    internal record class BreedingSolverStepState(
        int StepIndex,
        PalSpecifier Spec,
        WorkingSet WorkingSet,
        FrozenDictionary<PalId, ConcurrentDictionary<int, TimeSpan>> WorkingOptimalTimesByPalId
    );

    /// <summary>
    /// Main work processor for the breeding solver. Used to process a single batch of (parent, parent) pairs
    /// and return relevant children based on settings.
    /// </summary>
    /// <param name="controller">A controller which can be used to externally control the solver behavior, i.e. pause/resume.</param>
    /// <param name="settings">General settings for the solver process</param>
    /// <param name="poolFactory">An object pool factory used for frequently-created, transient objects.</param>
    internal class BreedingBatchSolver(
        SolverStateController controller,
        BreedingSolverSettings settings,
        ObjectPoolFactory poolFactory
    )
    {
        private PalDB db = settings.DB;

        private LocalListPool<PassiveSkill> passiveListPool = poolFactory.GetListPool<PassiveSkill>();
        private LocalListPool<(IPalReference, IPalReference)> palPairListPool = poolFactory.GetListPool<(IPalReference, IPalReference)>();
        private LocalObjectPool<IV_Set> ivSetPool = poolFactory.GetObjectPool<IV_Set>();

        // TODO - should be able to use an object pool for IV_Range
        static IV_IValue MergeIVs(IV_IValue a, IV_IValue b) =>
            (a, b) switch
            {
                (IV_IValue, IV_IValue) when a.IsRelevant && !b.IsRelevant => a,
                (IV_IValue, IV_IValue) when !a.IsRelevant && b.IsRelevant => b,

                (IV_IValue, IV_Random) => a,
                (IV_Random, IV_IValue) => b,

                (IV_Range va, IV_Range vb) => IV_Range.Merge(va, vb),
                _ => throw new NotImplementedException()
            };

        /// <summary>
        /// Creates a list of desired combinations of passives. Meant to handle the case where there are over MAX_PASSIVES desired passives.
        /// The `requiredPassives` should never have more than MAX_PASSIVES due to a check in `SolveFor`, so this logic is only really
        /// necessary if `requiredPassives` + `optionalPassives` brings us over MAX_PASSIVES.
        /// </summary>
        /// <param name="requiredPassives">The list of passives that will be contained in all permutations.</param>
        /// <param name="optionalPassives">The list of passives that will appear at least once across the permutations, if possible.</param>
        /// <returns></returns>
        static IEnumerable<IEnumerable<PassiveSkill>> PassiveSkillPermutations(List<PassiveSkill> requiredPassives, List<PassiveSkill> optionalPassives)
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
            if (optionalPassives.Count == 0 || requiredPassives.Count == GameConstants.MaxTotalPassives)
            {
                yield return requiredPassives;
                yield break;
            }

            var maxOptionalPassives = GameConstants.MaxTotalPassives - requiredPassives.Count();
            foreach (var optional in optionalPassives.Combinations(maxOptionalPassives))
            {
                var res = requiredPassives.Concat(optional);
                yield return res;
            }
        }

        TimeSpan CombinedEffort(IPalReference p1, IPalReference p2) =>
            settings.GameSettings.MultipleBreedingFarms
                ? p1.BreedingEffort > p2.BreedingEffort ? p1.BreedingEffort : p2.BreedingEffort
                : p1.BreedingEffort + p2.BreedingEffort;

        // we have two parents but don't necessarily have definite genders for them, figure out which parent should have which
        // gender (if they're wild/bred pals) for the least overall effort (different pals have different gender probabilities)
        (IPalReference, IPalReference) PreferredParentsGenders((IPalReference, IPalReference) p)
        {
            var (parent1, parent2) = p;

            var parentPairOptions = palPairListPool.Borrow();

            if (parent1.Gender == PalGender.WILDCARD)
            {
                if (parent2.Gender == PalGender.WILDCARD)
                {
                    parentPairOptions.Add((
                        parent1.WithGuaranteedGender(db, PalGender.MALE),
                        parent2.WithGuaranteedGender(db, PalGender.FEMALE)
                    ));
                    parentPairOptions.Add((
                        parent1.WithGuaranteedGender(db, PalGender.FEMALE),
                        parent2.WithGuaranteedGender(db, PalGender.MALE)
                    ));
                }
                else
                {
                    parentPairOptions.Add((
                        parent1.WithGuaranteedGender(db, parent2.Gender.OppositeGender()),
                        parent2
                    ));
                }
            }
            else if (parent2.Gender == PalGender.WILDCARD)
            {
                parentPairOptions.Add((
                    parent1,
                    parent2.WithGuaranteedGender(db, parent1.Gender.OppositeGender())
                ));
            }
            else if (parent1.Gender != parent2.Gender)
            {
                parentPairOptions.Add((parent1, parent2));
            }

            TimeSpan optimalTime = TimeSpan.Zero;
            bool hasNoPreference = true;
            foreach (var (p1, p2) in parentPairOptions)
            {
                var effort = CombinedEffort(p1, p2);
                if (optimalTime == TimeSpan.Zero) optimalTime = effort;
                else if (optimalTime != effort)
                {
                    hasNoPreference = false;

                    if (effort < optimalTime) optimalTime = effort;
                }
            }

            if (hasNoPreference)
            {
                palPairListPool.Return(parentPairOptions);

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
                foreach (var opt in parentPairOptions)
                {
                    if (optimalTime == CombinedEffort(opt.Item1, opt.Item2))
                    {
                        palPairListPool.Return(parentPairOptions);
                        return opt;
                    }
                }

                // shouldn't happen
                throw new NotImplementedException();
            }
        }

        // (actual method which processes parent pairs and collects their children)
        public IEnumerable<IPalReference> ProcessBatch(
            IEnumerable<(IPalReference, IPalReference)> workBatch,
            WorkBatchProgress progress,
            BreedingSolverStepState state
        )
        {
            var breedingdb = PalBreedingDB.LoadEmbedded(db);

            foreach (var p in workBatch)
            {
                controller.PauseIfRequested();
                if (controller.CancellationToken.IsCancellationRequested) yield break;

                progress.NumProcessed++;

                if (!p.Item1.IsCompatibleGender(p.Item2.Gender)) continue;
                if (p.Item1.NumWildPalParticipants() + p.Item2.NumWildPalParticipants() > settings.MaxWildPals) continue;
                if (p.Item1.NumTotalBreedingSteps + p.Item2.NumTotalBreedingSteps >= settings.MaxBreedingSteps) continue;

                {
                    // don't bother checking the child pal if it's impossible for them to reach the target within the remaining
                    // number of iterations
                    var breedingResults = breedingdb.BreedingByParent[p.Item1.Pal][p.Item2.Pal];
                    bool canReach = false;

                    foreach (var result in breedingResults)
                    {
                        if (breedingdb.MinBreedingSteps[result.Child][state.Spec.Pal] <= settings.MaxSolverIterations - state.StepIndex - 1)
                            canReach = true;
                    }

                    if (!canReach) continue;
                }

                // if we disallow any irrelevant passives, neither parents have a useful passive, and at least 1 parent
                // has an irrelevant passive, then it's impossible to breed a child with zero total passives
                //
                // (child would need to have zero since there's nothing useful to inherit and we disallow irrelevant passives,
                //  impossible to have zero since a child always inherits at least 1 direct passive if possible)
                if (settings.MaxBredIrrelevantPassives == 0)
                {
                    bool HasValidPassives(IPalReference parent)
                    {
                        foreach (var passive in parent.EffectivePassives)
                        {
                            if (state.Spec.RequiredPassives.Contains(passive)) return true;
                        }

                        return parent.EffectivePassives.Count == 0;
                    }

                    if (!HasValidPassives(p.Item1) && !HasValidPassives(p.Item2))
                        continue;
                }

                var ivsProbability = Probabilities.IVs.ProbabilityInheritedTargetIVs(p.Item1.IVs, p.Item2.IVs);

                var finalIVs = FIVSet.Merge(p.Item1.IVs, p.Item2.IVs);

                var parentPassives = passiveListPool.BorrowWith(p.Item1.ActualPassives);
                foreach (var passive in p.Item2.ActualPassives)
                    if (!parentPassives.Contains(passive))
                        parentPassives.Add(passive);

                var availableRequiredPassives = passiveListPool.Borrow();
                var availableOptionalPassives = passiveListPool.Borrow();

                foreach (var passive in parentPassives)
                {
                    if (state.Spec.RequiredPassives.Contains(passive))
                        availableRequiredPassives.Add(passive);

                    if (state.Spec.OptionalPassives.Contains(passive))
                        availableOptionalPassives.Add(passive);
                }

                // if both parents are wildcards, go through the list of possible gender-specific breeding results
                // and modify the parent genders to cover each possible child
                var expandedGendersByChildren = palPairListPool.Borrow();
                {
#if DEBUG && DEBUG_CHECKS
                    // (shouldn't happen)
                    if (parent1.Gender == PalGender.OPPOSITE_WILDCARD || parent2.Gender == PalGender.OPPOSITE_WILDCARD)
                        Debugger.Break();
#endif
                    if (p.Item1.Gender != PalGender.WILDCARD || p.Item2.Gender != PalGender.WILDCARD)
                    {
                        expandedGendersByChildren.Add(p);
                    }
                    else
                    {
                        foreach (var br in breedingdb.BreedingByParent[p.Item1.Pal][p.Item2.Pal])
                        {
                            expandedGendersByChildren.Add((
                                p.Item1.WithGuaranteedGender(db, br.RequiredGenderOf(p.Item1.Pal)),
                                p.Item2.WithGuaranteedGender(db, br.RequiredGenderOf(p.Item2.Pal))
                            ));
                        }
                    }
                }

                bool createdResult = false;
                foreach (var expanded in expandedGendersByChildren)
                {
                    if (controller.CancellationToken.IsCancellationRequested) yield break;

                    // arbitrary reordering of (p1, p2) to prevent duplicate results from swapped pairs
                    // (though this shouldn't be necessary if the `IResultPruning` impls are working right?)
                    var reexpanded = expanded.Item1.GetHashCode() > expanded.Item2.GetHashCode()
                        ? (expanded.Item2, expanded.Item1)
                        : expanded;

                    var pg = PreferredParentsGenders(reexpanded);

                    var (parent1, parent2) = pg;

                    Pal childPalType = null;
                    foreach (var br in breedingdb.BreedingByParent[parent1.Pal][parent2.Pal])
                        if (br.Matches(parent1.Pal, parent1.Gender, parent2.Pal, parent2.Gender))
                            childPalType = br.Child;

                    if (childPalType == null)
                        throw new NotImplementedException(); // shouldn't happen

                    if (settings.BannedBredPals.Contains(childPalType))
                        continue;

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

                    foreach (var rawTargetPassives in PassiveSkillPermutations(availableRequiredPassives, availableOptionalPassives))
                    {
                        var targetPassives = passiveListPool.BorrowWith(rawTargetPassives);

                        // go through each potential final number of passives, accumulate the probability of any of these exact options
                        // leading to the desired passives within MAX_IRRELEVANT_PASSIVES.
                        //
                        // we'll generate an option for each possible outcome of up to the max possible number of passives, where each
                        // option represents the likelyhood of getting all desired passives + up to some number of irrelevant passives
                        var probabilityForUpToNumPassives = 0.0f;

                        for (int numFinalPassives = targetPassives.Count; numFinalPassives <= Math.Min(GameConstants.MaxTotalPassives, targetPassives.Count + settings.MaxBredIrrelevantPassives); numFinalPassives++)
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
                            var newPassives = passiveListPool.BorrowWith(targetPassives);
                            while (newPassives.Count < numFinalPassives)
                                newPassives.Add(new RandomPassiveSkill());

                            var res = new BredPalReference(
                                settings.GameSettings,
                                childPalType,
                                parent1,
                                parent2,
                                newPassives,
                                probabilityForUpToNumPassives,
                                finalIVs,
                                ivsProbability
                            );

                            var workingOptimalTimes = state.WorkingOptimalTimesByPalId[res.Pal.Id];

                            var added = false;
                            var effort = res.BreedingEffort;
                            if (effort <= settings.MaxEffort && (state.Spec.IsSatisfiedBy(res) || state.WorkingSet.IsOptimal(res)))
                            {
                                var resultId = WorkingSet.DefaultGroupFn(res);

                                bool updated = workingOptimalTimes.TryAdd(resultId, effort);
                                while (!updated)
                                {
                                    var v = workingOptimalTimes[resultId];
                                    if (v < effort) break;

                                    updated = workingOptimalTimes.TryUpdate(resultId, effort, v);
                                }

                                if (updated && res.BreedingEffort <= settings.MaxEffort)
                                {
                                    yield return res;
                                    createdResult = true;
                                    added = true;
                                }
                            }

                            if (!added)
                                passiveListPool.Return(newPassives);
                        }

                        passiveListPool.Return(targetPassives);
                    }
                }

                palPairListPool.Return(expandedGendersByChildren);

                passiveListPool.Return(parentPassives);
                passiveListPool.Return(availableRequiredPassives);
                passiveListPool.Return(availableOptionalPassives);
            }
        }
    }
}
