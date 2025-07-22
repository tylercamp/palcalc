using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using Serilog;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;

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

        public long CurrentWorkSize { get; set; }

        public long WorkProcessedCount { get; set; }
        public long TotalWorkProcessedCount { get; set; }

        public override int GetHashCode() => HashCode.Combine(CurrentPhase, CurrentStepIndex, Canceled, Paused, CurrentWorkSize, WorkProcessedCount);
    }

    public class BreedingSolver(BreedingSolverSettings settings)
    {
        private static ILogger logger = Log.ForContext<BreedingSolver>();

        public event Action<SolverStatus> SolverStateUpdated;
        public TimeSpan SolverStateUpdateInterval { get; set; } = TimeSpan.FromMilliseconds(100);

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
            static IV_Range MakeIV(int minValue, int value) =>
                new(
                    isRelevant: minValue != 0 && value >= minValue,
                    value: value
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

            if (!settings.OptimizeInitStep)
            {
                initialContent = initialContent.Concat(
                    settings.OwnedPals.Select(
                        p => new OwnedPalReference(
                            p,
                            p.PassiveSkills.ToDedicatedPassives(spec.DesiredPassives),
                            new IV_Set() { HP = MakeIV(spec.IV_HP, p.IV_HP), Attack = MakeIV(spec.IV_Attack, p.IV_Attack), Defense = MakeIV(spec.IV_Defense, p.IV_Defense) }
                        )
                    )
                ).Distinct().ToList();
            }

            if (settings.MaxWildPals > 0)
            {
                // add wild pals with varying number of random passives
                initialContent.AddRange(
                    settings.AllowedWildPals
                        .Where(p => !settings.OwnedPals.Any(i => i.Pal == p))
                        .Where(p => WithinBreedingSteps(p, settings.MaxBreedingSteps))
                        .SelectMany(p =>
                            Enumerable
                                .Range(
                                    0,
                                    // number of "effectively random" passives should exclude guaranteed passives which are part of the desired list of passives
                                    Math.Max(
                                        0,
                                        settings.MaxInputIrrelevantPassives - p.GuaranteedPassiveSkills(settings.DB).Except(spec.DesiredPassives).Count()
                                    )
                                )
                                .Select(numRandomPassives => new WildPalReference(p, p.GuaranteedPassiveSkills(settings.DB), numRandomPassives))
                        )
                        .Where(pi => pi.BreedingEffort <= settings.MaxEffort)
                );
            }

            return initialContent;
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
                TargetSteps = settings.MaxSolverIterations,
                Canceled = controller.CancellationToken.IsCancellationRequested,
                Paused = controller.IsPaused,
            };
            SolverStateUpdated?.Invoke(statusMsg);

            var workingSet = new WorkingSet(spec, settings.PruningBuilder, BuildInitialContent(spec), settings.MaxThreads, controller);
            var tlPoolFactory = new ThreadLocal<ObjectPoolFactory>(() => new ObjectPoolFactory());

            for (int s = 0; s < settings.MaxSolverIterations; s++)
            {
                if (controller.CancellationToken.IsCancellationRequested) break;

                var stepState = new BreedingSolverStepState(
                    StepIndex: s,
                    Spec: spec,
                    WorkingSet: workingSet,
                    WorkingOptimalTimesByPalId: settings.DB.PalsById.Keys.ToFrozenDictionary(id => id, _ => new ConcurrentDictionary<int, BreedingSolverEfficiencyMetric>())
                );
                List<WorkBatchProgress> progressEntries = [];

                // Phase 1: Breeding pairs (existing logic)
                bool didBreed = workingSet.UpdateByPairs(work =>
                {
                    logger.Debug("Performing breeding step {step} with {numWork} work items", s+1, work.Count);

                    statusMsg.CurrentPhase = SolverPhase.Breeding;
                    statusMsg.CurrentStepIndex = s;
                    statusMsg.Canceled = controller.CancellationToken.IsCancellationRequested;
                    statusMsg.CurrentWorkSize = work.Count;
                    statusMsg.WorkProcessedCount = 0;
                    SolverStateUpdated?.Invoke(statusMsg);

                    int lastMsgHash = 0;
                    void EmitProgressMsg(object _)
                    {
                        lock (progressEntries)
                        {
                            var progress = progressEntries.Sum(e => e.NumProcessed);
                            statusMsg.WorkProcessedCount = progress;
                        }
                        statusMsg.Paused = controller.IsPaused;
                        statusMsg.Canceled = controller.CancellationToken.IsCancellationRequested;

                        if (lastMsgHash == 0 || lastMsgHash != statusMsg.GetHashCode())
                        {
                            lastMsgHash = statusMsg.GetHashCode();
                            SolverStateUpdated?.Invoke(statusMsg);
                        }
                    }

                    var progressTimer = new Timer(EmitProgressMsg, null, (int)SolverStateUpdateInterval.TotalMilliseconds, (int)SolverStateUpdateInterval.TotalMilliseconds);

                    var chunksEnumerator = work.Chunks(work.Count.PreferredParallelBatchSize()).GetEnumerator();
                    var results = new ConcurrentBag<List<IPalReference>>();

                    // specifically avoiding AsParallel so we don't congest the default threadpool and so we can
                    // set a lower priority for these threads
                    var workThreads = Enumerable
                        .Range(0, settings.MaxThreads)
                        .Select(_ => new Thread(() =>
                        {
                            while (true)
                            {
                                IEnumerable<(IPalReference, IPalReference)> batch = null;
                                lock(chunksEnumerator)
                                {
                                    if (!chunksEnumerator.MoveNext())
                                        break;

                                    batch = chunksEnumerator.Current;
                                }

                                var progress = new WorkBatchProgress();
                                lock (progressEntries)
                                    progressEntries.Add(progress);

                                var batchSolver = new BreedingBatchSolver(controller, settings, tlPoolFactory.Value);
                                results.Add(batchSolver.ProcessBatch(batch, progress, stepState).ToList());
                            }
                        }))
                        .ToList();

                    foreach (var thread in workThreads)
                    {
                        thread.Priority = ThreadPriority.BelowNormal;
                        thread.Start();
                    }

                    foreach (var thread in workThreads)
                    {
                        thread.Join();
                    }

                    var res = results.SelectMany(l => l).ToList();
                    progressTimer.Dispose();

                    lock (progressEntries)
                        statusMsg.WorkProcessedCount = progressEntries.Sum(e => e.NumProcessed);

                    statusMsg.TotalWorkProcessedCount += statusMsg.WorkProcessedCount;
                    statusMsg.Canceled = controller.CancellationToken.IsCancellationRequested;
                    SolverStateUpdated?.Invoke(statusMsg);

                    return res;
                });

                // Phase 2: Surgery additions
                bool didSurgery = false;
                var surgeryCompatiblePassives = spec.DesiredPassives.Where(p => p.SupportsSurgery).Where(settings.SurgeryPassives.Contains).ToList();
                if (surgeryCompatiblePassives.Any() && !controller.CancellationToken.IsCancellationRequested)
                {
                    // TODO - sort out gender-swap options?

                    didSurgery = workingSet.UpdateBySingle(palRefs =>
                    {
                        return palRefs
                            .Where(r =>
                                // there's room for a new passive
                                r.EffectivePassives.Count < GameConstants.MaxTotalPassives ||
                                // there's a replaceable passive
                                r.EffectivePassives.Any(p => p is RandomPassiveSkill)
                            )
                            .ToList()
                            .BatchedForParallel()
                            // (this should be relatively quick, will just use normal AsParallel)
                            .AsParallel()
                            .WithDegreeOfParallelism(settings.MaxThreads)
                            .SelectMany(batch => batch.SelectMany(r =>
                            {
                                // TODO: atm won't consider replacing Optional passives, even though they're technically not Required

                                var missingPassives = surgeryCompatiblePassives.Except(r.EffectivePassives).ToList();
                                var maxNewPassives = GameConstants.MaxTotalPassives - r.EffectivePassives.Count(p => p is not RandomPassiveSkill);

                                var res = new List<IPalReference>();

                                // consider all ways we could add these desired passives
                                foreach (var passives in missingPassives.Combinations(maxNewPassives).Select(c => c.ToList()).Where(c => c.Count > 0).ToList().OrderByDescending(c => c.Count))
                                {
                                    if (passives.Count == 0)
                                        continue;

                                    if (r.TotalCost + passives.Sum(p => p.SurgeryCost) > settings.MaxSurgeryCost)
                                        continue;

                                    var removable = new Queue<PassiveSkill>(r.EffectivePassives.OfType<RandomPassiveSkill>());
                                    var ops = new List<ISurgeryOperation>();
                                    for (int i = 0; i < passives.Count; i++)
                                    {
                                        // adding this would go over the total passive limit, use a Replacement operation
                                        if (r.EffectivePassives.Count + i >= GameConstants.MaxTotalPassives)
                                        {
                                            if (!removable.Any())
                                            {
#if DEBUG_CHECKS
                                                Debugger.Break();
#endif
                                                break;
                                            }

                                            var toRemove = removable.Dequeue();
                                            ops.Add(ReplacePassiveSurgeryOperation.NewCached(toRemove, passives[i]));
                                        }
                                        else // there's space for another passive, use an Add operation
                                        {
                                            ops.Add(AddPassiveSurgeryOperation.NewCached(passives[i]));
                                        }
                                    }

                                    res.Add(SurgeryTablePalReference.NewCached(r, ref ops));
                                }

                                return res.Where(re => re != r);
                            }));
                    });
                }

                if (controller.CancellationToken.IsCancellationRequested) break;

                if (!didBreed && !didSurgery)
                {
                    logger.Debug("Last pass found no new useful options, stopping iteration early");
                    break;
                }
            }

            statusMsg.Canceled = controller.CancellationToken.IsCancellationRequested;
            statusMsg.CurrentPhase = SolverPhase.Finished;
            SolverStateUpdated?.Invoke(statusMsg);

            return workingSet
                .Result
                // the breeding logic will never emit pals which exceed this limit, but this isn't applied for owned pals
                // which already satisfy the pal specifier
                .Where(r => r.ActualPassives.Except(spec.DesiredPassives).Count() <= settings.MaxBredIrrelevantPassives)
                .Select(r =>
                {
                    if (spec.RequiredGender != PalGender.WILDCARD)
                        return r.WithGuaranteedGender(settings.DB, spec.RequiredGender);
                    else
                        return r;

                }).ToList();
        }
    }
}
