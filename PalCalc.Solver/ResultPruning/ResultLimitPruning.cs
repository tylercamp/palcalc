using PalCalc.Solver.PalReference;
using PalCalc.Solver.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class ResultLimitPruning : ResultPruningRule.ForceDeterministic
    {
        private readonly int maxResults;
        public ResultLimitPruning(CancellationToken token, int maxResults) : base(token)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);
            this.maxResults = maxResults;
        }

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            results.TakeUntilCancelled(token).Take(maxResults);
    }
}
