using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Probabilities;
using PalCalc.Solver.ResultPruning;
using Serilog;
using System.Diagnostics;

namespace PalCalc.Solver
{
    internal sealed class SolverRun(
        SolverRunContext context,
        Action<SolverStatus> stateUpdated,
        TimeSpan stateUpdateInterval
    )
    {
        private static ILogger logger = Log.ForContext<BreedingSolver>();

        private BreedingSolverSettings settings => context.Settings;

        /// <summary>
        /// Builds the initial set of pals to breed based on the provided target pal.
        /// 
        /// Maps pals from the save file into solver-specific types, removes duplicates, and inserts wild pals.
        /// </summary>
        private IEnumerable<IPalReference> BuildInitialContent(PalSpecifier spec)
        {
            var breedingdb = PalBreedingDB.LoadEmbedded(settings.DB);

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

            bool WithinBreedingSteps(Pal pal, int maxSteps) => breedingdb.MinBreedingSteps[pal][spec.Pal] <= maxSteps;
            static IV_Value MakeIV(int minValue, int value) =>
                new(
                    IsRelevant: minValue != 0 && value >= minValue,
                    Min: value,
                    Max: value
                );

            var initialContent = settings.OwnedPals
                // skip pals if they can't be used to reach the desired pals (e.g. Jetragon can only be bred from other Jetragons)
                .Where(p => WithinBreedingSteps(p.Pal, settings.MaxBreedingSteps))
                // apply "Max Input Irrelevant Passives" setting
                .Where(p => p.PassiveSkills.Except(spec.DesiredPassives).Count() <= settings.MaxInputIrrelevantPassives)
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

            if (settings.MaxWildPals > 0)
            {
                // add wild pals with varying number of random passives
                initialContent.AddRange(
                    settings.AllowedWildPals
                        .Where(p => !settings.OwnedPals.Any(i => i.Pal == p))
                        .Where(p => WithinBreedingSteps(p, settings.MaxBreedingSteps))
                        .SelectMany(p =>
                        {
                            var guaranteedPassives = p.GuaranteedPassiveSkills(settings.DB);
                            var numIrrelevantGuaranteed = guaranteedPassives.Except(spec.DesiredPassives).Count();

                            return Enumerable
                                .Range(
                                    0,
                                    // number of "effectively random" passives should exclude guaranteed passives which are part of the desired list of passives
                                    Math.Clamp(
                                        value: settings.MaxInputIrrelevantPassives - numIrrelevantGuaranteed,
                                        min: numIrrelevantGuaranteed > settings.MaxInputIrrelevantPassives ? 0 : 1,
                                        max: GameConstants.MaxTotalPassives - guaranteedPassives.Count()
                                    )
                                )
                                .Select(numRandomPassives => new WildPalReference(p, guaranteedPassives, numRandomPassives));
                        })
                        .Where(pi => pi.BreedingEffort <= settings.MaxEffort)
                );
            }

            return initialContent;
        }

        public List<IPalReference> Execute()
        {
            var spec = context.Target;
            var controller = context.Controller;

            if (spec.RequiredPassives.Count > GameConstants.MaxTotalPassives)
            {
                throw new Exception("Target passive skill count cannot exceed max number of passive skills for a single pal");
            }

            var statusMsg = new SolverStatus()
            {
                CurrentPhase = SolverPhase.Initializing,
                CurrentStepIndex = 0,
                TargetSteps = settings.MaxSolverIterations,
                Canceled = controller.CancellationToken.IsCancellationRequested,
                Paused = controller.IsPaused,
            };
            stateUpdated?.Invoke(statusMsg);

            var frontier = new SearchFrontier(
                spec,
                BuildInitialContent(spec),
                settings.MaxThreads,
                controller,
                context.SelectionPolicy
            );
            var batchExecutor = new ParallelBatchExecutor(context, stateUpdateInterval);

            // Apply main set of breeding passes

            for (int s = 0; s < settings.MaxSolverIterations; s++)
            {
                if (controller.CancellationToken.IsCancellationRequested) break;

                var expansionContext = new CandidateExpansionContext(
                    StepIndex: s,
                    Target: spec,
                    Admissions: new CandidateAdmissionState(
                        target: spec,
                        maxEffort: settings.MaxEffort,
                        selectionPolicy: context.SelectionPolicy,
                        frontier: frontier,
                        palIds: settings.DB.PalsById.Keys
                    )
                );

                var delta = frontier.ExpandPairs(work =>
                {
                    logger.Debug("Performing breeding step {step} with {numWork} work items", s+1, work.Count);

                    statusMsg = statusMsg with
                    {
                        CurrentPhase = SolverPhase.Breeding,
                        CurrentStepIndex = s,
                        Canceled = controller.CancellationToken.IsCancellationRequested,
                        Paused = controller.IsPaused,
                        CurrentWorkSize = work.Count,
                        WorkProcessedCount = 0,
                    };
                    stateUpdated?.Invoke(statusMsg);

                    var execution = batchExecutor.Execute(
                        work,
                        expansionContext,
                        progress =>
                        {
                            statusMsg = statusMsg with
                            {
                                WorkProcessedCount = progress.WorkProcessedCount,
                                Paused = progress.Paused,
                                Canceled = progress.Canceled,
                            };
                            stateUpdated?.Invoke(statusMsg);
                        }
                    );

                    statusMsg = statusMsg with
                    {
                        WorkProcessedCount = execution.WorkProcessedCount,
                        TotalWorkProcessedCount =
                            statusMsg.TotalWorkProcessedCount +
                            execution.WorkProcessedCount,
                        Canceled = controller.CancellationToken.IsCancellationRequested,
                        Paused = controller.IsPaused,
                    };
                    stateUpdated?.Invoke(statusMsg);

                    return execution.Candidates;
                });

                if (controller.CancellationToken.IsCancellationRequested) break;

                if (!delta.Changed)
                {
                    logger.Debug("Last pass found no new useful options, stopping iteration early");
                    break;
                }
            }

            // (Main breeding pass done)

            statusMsg = statusMsg with
            {
                Canceled = controller.CancellationToken.IsCancellationRequested,
                Paused = controller.IsPaused,
                CurrentPhase = SolverPhase.Finished,
            };
            stateUpdated?.Invoke(statusMsg);

            // Do another pass which performs surgery to add desired passives to all final pals. We only do this *after* the
            // main breeding pass - applying it with each breeding pass would _technically_ be more accurate, since it would
            // allow replacement of irrelevant passives with relevant passives which can affect breeding time estimates.
            //
            // Theoretically, there's some case where this would give a faster result. However, in practice, optimal paths
            // almost always have passive-surgery ops at the end, and including surgery after each breeding pass significantly
            // affects compute time.
            
            var surgeryCompatiblePassives = spec.DesiredPassives.Where(p => p.SupportsSurgery).Where(settings.SurgeryPassives.Contains).ToList();
            if (surgeryCompatiblePassives.Any() && !controller.CancellationToken.IsCancellationRequested)
            {
                frontier.ExpandSingles(palRefs => palRefs
                    .Where(r => r.Pal == spec.Pal)
                    .Where(r =>
                        // there's room for a new passive
                        r.EffectivePassives.Count < GameConstants.MaxTotalPassives ||
                        // there's a replaceable irrelevant passive
                        r.EffectivePassives.Any(p => p is RandomPassiveSkill) ||
                        // there's a replaceable optional passive
                        r.EffectivePassives.Any(p => spec.OptionalPassives.Contains(p))
                    )
                    .TakeUntilCancelled(controller.CancellationToken)
                    // 1. Try to add the remaining Required passives to the pal
                    .Select(r =>
                    {
                        var missingRequiredPassives = surgeryCompatiblePassives.Where(spec.RequiredPassives.Contains).Except(r.EffectivePassives).ToList();
                        // if adding the required passives causes us to exceed the max surgery cost, then there's no way
                        // to make this pal meet the full requirements, and it can be skipped
                        if (missingRequiredPassives.Count == 0 || missingRequiredPassives.Sum(p => p.SurgeryCost) + r.TotalCost > settings.MaxSurgeryCost)
                            return r;

                        var removeablePassives = new Queue<PassiveSkill>(r.EffectivePassives.OfType<RandomPassiveSkill>());

                        foreach (var optional in r.EffectivePassives.Where(spec.OptionalPassives.Contains))
                            removeablePassives.Enqueue(optional);

                        var ops = new List<ISurgeryOperation>();
                        var modifiedPassives = new List<PassiveSkill>(r.EffectivePassives);

                        foreach (var toAdd in missingRequiredPassives)
                        {
                            if (modifiedPassives.Count < GameConstants.MaxTotalPassives)
                            {
                                modifiedPassives.Add(toAdd);
                                ops.Add(AddPassiveSurgeryOperation.NewCached(toAdd));
                            }
                            else if (removeablePassives.TryDequeue(out var toRemove))
                            {
                                modifiedPassives.Remove(toRemove);
                                modifiedPassives.Add(toAdd);
                                ops.Add(ReplacePassiveSurgeryOperation.NewCached(toRemove, toAdd));
                            }
                        }

                        return new SurgeryTablePalReference(r, ops);
                    })
                    // 2. Fill remaining non-desired slots with Optional passives
                    .SelectMany(r =>
                    {
                        var missingOptionalPassives = surgeryCompatiblePassives.Where(spec.OptionalPassives.Contains).Except(r.EffectivePassives).ToList();
                        var numAddablePassives = GameConstants.MaxTotalPassives - r.EffectivePassives.Count(p => p is not RandomPassiveSkill);

                        // (we're still in the same ExpandSingles pass, just the 2nd part. the pals from the previous pass haven't been stored yet, so make sure
                        // to include them in the list of results for this pass)
                        var res = new List<IPalReference>() { r };

                        foreach (
                            var passives in missingOptionalPassives
                                .Combinations(numAddablePassives, null)
                                .Where(p => r.TotalCost + p.Sum(i => i.SurgeryCost) <= settings.MaxSurgeryCost)
                                .Where(p => p.Any())
                                .Select(l => l.ToList())
                        )
                        {
                            var removablePassives = new Queue<PassiveSkill>(r.EffectivePassives.OfType<RandomPassiveSkill>());
                            var modifiedPassives = new List<PassiveSkill>(r.EffectivePassives);

                            var ops = new List<ISurgeryOperation>();
                            foreach (var toAdd in passives)
                            {
                                if (modifiedPassives.Count < GameConstants.MaxTotalPassives)
                                {
                                    modifiedPassives.Add(toAdd);
                                    ops.Add(AddPassiveSurgeryOperation.NewCached(toAdd));
                                }
                                else if (removablePassives.TryDequeue(out var toRemove))
                                {
                                    modifiedPassives.Remove(toRemove);
                                    modifiedPassives.Add(toAdd);
                                    ops.Add(ReplacePassiveSurgeryOperation.NewCached(toRemove, toAdd));
                                }
                                else
                                {
#if DEBUG_CHECKS
                                    Debugger.Break();
#endif
                                }
                            }

                            res.Add(new SurgeryTablePalReference(r, ops));
                        }

                        return res;
                    })
                );

            }

            IEnumerable<IPalReference> EnforceRequiredGender(IPalReference input)
            {
                if (spec.RequiredGender != PalGender.WILDCARD && input.Gender != spec.RequiredGender)
                {
                    // pals with indeterminate gender can naturally be forced to a specific gender
                    // pals with a specific gender can also be forced with surgery if enabled
                    if (input.Gender == PalGender.WILDCARD || settings.UseGenderReversers)
                        yield return input.WithGuaranteedGender(settings.DB, spec.RequiredGender, settings.UseGenderReversers);

                    // otherwise this pal doesn't have the required gender and can't be forced to that
                    // gender, skip it
                }
                else
                {
                    // no need to enforce gender, or pal is already the required gender
                    yield return input;
                }

            }
            return frontier
                .Results
                // the breeding logic will never emit pals which exceed this limit, but this isn't applied for owned pals
                // which already satisfy the pal specifier
                .Where(r => r.ActualPassives.Except(spec.DesiredPassives).Count() <= settings.MaxBredIrrelevantPassives)
                .SelectMany(EnforceRequiredGender)
                .Distinct()
                .ToList();
        }
    }
}
