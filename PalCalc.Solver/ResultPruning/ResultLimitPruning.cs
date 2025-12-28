using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class ResultLimitPruning : IResultPruning.ForceDeterministic
    {
        int maxResults;
        public ResultLimitPruning(CancellationToken token, int maxResults) : base(token)
        {
            this.maxResults = maxResults;
        }

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            results.TakeUntilCancelled(token).Take(maxResults);
    }
}
