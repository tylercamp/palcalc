using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    // a working set of pal instances to be used as potential parents
    internal class WorkingSet
    {
        private static ILogger logger = Log.ForContext<WorkingSet>();

        private HashSet<IPalReference> content;

        public IReadOnlySet<IPalReference> Content => content;

        public WorkingSet()
        {
            content = new HashSet<IPalReference>();
        }

        public WorkingSet(IEnumerable<IPalReference> initialContent)
        {
            content = new HashSet<IPalReference>();
            AddFrom(initialContent, CancellationToken.None);
        }

        // returns the number of entries added/updated
        public int AddFrom(IEnumerable<IPalReference> newRefs, CancellationToken token)
        {
            logger.Debug("updating working set");

            // since we know the breeding effort of each potential instance, we can ignore new instances
            // with higher effort than existing known instances
            //
            // (this is the main optimization that lets the solver complete in less than a week)

            // `PruneCollection` is fairly heavy and single-threaded, perform pruning of multiple batches of the
            // main set of references before pruning the final combined collection

            logger.Debug("performing pre-prune");
            var prePruned = newRefs.ToList().Batched(100000)
                .AsParallel()
                .SelectMany(batch => PruneCollection(batch).ToList())
                .ToList();

            logger.Debug("merging");
            var numChanged = 0;
            foreach (var newInst in PruneCollection(prePruned))
            {
                var existingInstances = content
                    .TakeWhile(_ => !token.IsCancellationRequested)
                    .Where(pi =>
                        pi.Pal == newInst.Pal &&
                        pi.Gender == newInst.Gender &&
                        pi.Traits.EqualsTraits(newInst.Traits)
                    )
                    .ToList();

                if (token.IsCancellationRequested) return numChanged;

                var existingInst = existingInstances.SingleOrDefault();

                if (existingInst != null)
                {
                    if (newInst.BreedingEffort < existingInst.BreedingEffort)
                    {
                        content.Remove(existingInst);
                        content.Add(newInst);
                        numChanged++;
                    }
                }
                else
                {
                    content.Add(newInst);
                    numChanged++;
                }
            }

            logger.Debug("done, {numChanged} changed", numChanged);
            return numChanged;
        }

        public int AddFrom(WorkingSet ws, CancellationToken token) => AddFrom(ws.Content, token);

        // gives a new, reduced collection which only includes the "most optimal" / lowest-effort
        // reference for each instance spec (gender, traits, etc.)
        private static IEnumerable<IPalReference> PruneCollection(IEnumerable<IPalReference> refs) =>
            refs
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
                .Select(g => g
                    .OrderBy(pref => pref.BreedingEffort)
                    .ThenBy(pref => pref.NumTotalBreedingSteps)
                    .First()
                );
    }
}
