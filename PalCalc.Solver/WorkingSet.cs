using PalCalc.Model;
using PalCalc.Solver.PalReference;
using Serilog;

namespace PalCalc.Solver
{
    internal class WorkingSet
    {
        private static ILogger logger = Log.ForContext<WorkingSet>();

        private readonly SolverStateController controller;
        private readonly ICandidateSelectionPolicy selectionPolicy;
        private readonly BreedingStateIndex content;
        private ILazyCartesianProduct<IPalReference> remainingParentPairs;

        private readonly int maxThreads;
        private readonly PalSpecifier target;
        private readonly List<IPalReference> discoveredResults = [];
        public IEnumerable<IPalReference> Result
        {
            get
            {
                return discoveredResults
                    .Distinct()
                    .GroupBy(selectionPolicy.ResultTierOf)
                    .SelectMany(group =>
                        selectionPolicy.SelectRetainedAlternatives(group)
                    );
            }
        }
        
        public IEnumerable<IPalReference> CurrentContent => content.All;

        public BreedingStateIndex CurrentStateIndex => content;

        public WorkingSet(
            PalSpecifier target,
            IEnumerable<IPalReference> initialContent,
            int maxThreads,
            SolverStateController controller,
            ICandidateSelectionPolicy selectionPolicy
        )
        {
            ArgumentNullException.ThrowIfNull(selectionPolicy);

            this.target = target;
            this.controller = controller;
            this.selectionPolicy = selectionPolicy;

            content = new BreedingStateIndex(this.selectionPolicy);
            content.AddRange(initialContent);

            discoveredResults.AddRange(content.All.Where(target.IsSatisfiedBy));

            var initialList = initialContent.ToList();
            remainingParentPairs = new LazyCartesianProduct<IPalReference>(initialList, initialList);

            if (maxThreads <= 0) maxThreads = Environment.ProcessorCount;

            this.maxThreads = maxThreads;
        }

        public bool IsFrontierImprovement(
            IPalReference reference,
            BreedingStateKey stateKey
        )
        {
            var match = content[stateKey]?.FirstOrDefault();
            return match == null ||
                selectionPolicy.IsFrontierImprovement(reference, match);
        }

        /// <summary>
        /// Uses the provided `doWork` function to produce results for all remaining pair-wise work to be done. The results
        /// returned by `doWork` are merged with the current working set of results and the next set of work
        /// is updated.
        /// </summary>
        /// <param name="doWork"></param>
        /// <returns>Whether or not any changes were made by merging the current working set with the results of `doWork`.</returns>
        public bool UpdateByPairs(Func<ILazyCartesianProduct<IPalReference>, IEnumerable<IPalReference>> doWork)
        {
            if (remainingParentPairs.Count == 0) return false;

            logger.Debug("beginning pairs processing");

            var newResults = doWork(remainingParentPairs).ToList();

            var existingContent = content.All.OrderBy(p => p.Pal.Id).ToList();
            var changeset = MergeWithResults(newResults);
            existingContent.RemoveAll(changeset.Removed.Contains);
            
            remainingParentPairs = new ConcatenatedLazyCartesianProduct<IPalReference>([
                (
                    existingContent
                        .OrderBy(
                            reference => reference,
                            selectionPolicy.ExpansionPriorityComparer
                        )
                        .ToList(),
                    changeset.Added
                ),
                (changeset.Added, changeset.Added)
            ]);

            foreach (var ta in changeset.Added.TakeUntilCancelled(controller.CancellationToken))
            {
                if (controller.IsPaused) controller.PauseIfRequested();
                content.Add(ta);
            }

            return changeset.Changed;
        }

        /// <summary>
        /// Uses the provided `doWork` function to produce results for each individual `IPalReference`. The results
        /// returned by `doWork` are merged with the current working set of results and the next set of work
        /// is updated.
        /// </summary>
        /// <returns>Whether any changes were made</returns>
        public bool UpdateBySingle(Func<IEnumerable<IPalReference>, IEnumerable<IPalReference>> doWork)
        {
            logger.Debug("beginning single-item processing");

            var newItems = doWork(content.All).ToList();
            if (!newItems.Any()) return false;

            var existingContent = content
                .All
                .OrderBy(
                    reference => reference,
                    selectionPolicy.ExpansionPriorityComparer
                )
                .ToList();
            var changeset = MergeWithResults(newItems);
            existingContent.RemoveAll(changeset.Removed.Contains);

            remainingParentPairs = new ConcatenatedLazyCartesianProduct<IPalReference>([
                remainingParentPairs.Where(parent => !changeset.Removed.Contains(parent), controller.CancellationToken),
                new AntiDiagonalLazyCartesianProduct<IPalReference>(changeset.Added, existingContent),
                new AntiDiagonalLazyCartesianProduct<IPalReference>(changeset.Added, changeset.Added)
            ]);

            foreach (var ta in changeset.Added.TakeUntilCancelled(controller.CancellationToken))
            {
                if (controller.IsPaused) controller.PauseIfRequested();
                content.Add(ta);
            }

            return changeset.Changed;
        }

        // gives a new, reduced collection which only includes the "most optimal" / lowest-effort
        // reference for each instance spec (gender, passives, etc.)
        private IEnumerable<IPalReference> PruneCollection(IEnumerable<IPalReference> refs) =>
            refs
                .TakeWhile(r =>
                {
                    if (controller.IsPaused) controller.PauseIfRequested();
                    return !controller.CancellationToken.IsCancellationRequested;
                })
                .GroupBy(selectionPolicy.KeyOf)
                .SelectMany(g =>
                    selectionPolicy.SelectRetainedAlternatives(g)
                );

        private record class MergeChangeset(bool Changed, List<IPalReference> Added, HashSet<IPalReference> Removed);

        private MergeChangeset MergeWithResults(List<IPalReference> newResults)
        {
            var changed = false;
            var allAdded = new List<IPalReference>();
            var allRemoved = new List<IPalReference>();

            // since we know the breeding effort of each potential instance, we can ignore new instances
            // with higher effort than existing known instances
            //
            // (this is the main optimization that lets the solver complete in less than a week)

            // `PruneCollection` is fairly heavy and single-threaded, perform pruning of multiple batches of the
            // main set of references before pruning the final combined collection

            discoveredResults.AddRange(
                newResults
                    .TakeWhile(_ =>
                    {
                        if (controller.IsPaused) controller.PauseIfRequested();
                        return !controller.CancellationToken.IsCancellationRequested;
                    })
                    .Where(target.IsSatisfiedBy)
            );
            if (controller.CancellationToken.IsCancellationRequested) return new MergeChangeset(false, [], []);

            logger.Debug("performing pre-prune on {count} items", newResults.Count);
            var pruned = PruneCollection(
                newResults
                    .BatchedAsParallel()
                    .WithCancellation(controller.CancellationToken)
                    .WithDegreeOfParallelism(maxThreads)
                    .SelectMany(batch => PruneCollection(batch).ToList())
                    .ToList()
            );
            if (controller.CancellationToken.IsCancellationRequested) return new MergeChangeset(false, [], []);

            logger.Debug("merging");

            foreach (
                var newGroup in pruned
                    .GroupBy(selectionPolicy.KeyOf)
                    .ToList()
            )
            {
                if (controller.CancellationToken.IsCancellationRequested) return new MergeChangeset(changed, [], []);
                if (controller.IsPaused) controller.PauseIfRequested();

                var newInstances = newGroup.ToList();
                var refNewInst = newInstances.First();

                // these are results to be used as output, don't bother adding them to working set / continue breeding those
                if ((refNewInst is BredPalReference || refNewInst is SurgeryTablePalReference) && target.IsSatisfiedBy(refNewInst))
                {
                    // (though if we're not at the passive limit and there are some optional passives
                    //  we'd like, then we'll keep this in the pool)
                    if (
                        // at max passives
                        refNewInst.EffectivePassives.Count(t => t is not RandomPassiveSkill) == GameConstants.MaxTotalPassives ||
                        // there's nothing else we'd be interested in
                        !target.OptionalPassives.Except(refNewInst.EffectivePassives).Any()
                    ) continue;
                }

                var existingInstances = content[newGroup.Key];
                var refInst = existingInstances?.FirstOrDefault();

                if (refInst != null)
                {
                    var allInstances = existingInstances.Concat(newInstances.Except(existingInstances));
                    var newSelection =
                        selectionPolicy.SelectRetainedAlternatives(allInstances);

                    var added = newInstances.Intersect(newSelection).Except(existingInstances);
                    var removed = existingInstances.Except(newSelection);

                    if (added.Any())
                    {
                        allAdded.AddRange(added);
                        changed = true;
                    }

                    if (removed.Any())
                    {
                        foreach (var r in removed.ToList())
                        {
                            content.Remove(r);
                            allRemoved.Add(r);
                        }
                        changed = true;
                    }
                }
                else
                {
                    allAdded.AddRange(newInstances);
                    changed = true;
                }
            }

            
            // Apply the policy's expansion priority. With the default policy,
            // newly-discovered efficient children are processed early so they
            // can invalidate less efficient parents.
            allAdded = allAdded
                .OrderBy(
                    reference => reference,
                    selectionPolicy.ExpansionPriorityComparer
                )
                .ToList();

            return new MergeChangeset(changed, allAdded, new HashSet<IPalReference>(allRemoved));
        }
    }
}
