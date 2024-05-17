using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class ResultLimitPruning : IResultPruning
    {
        int maxResults;
        public ResultLimitPruning(CancellationToken token, int maxResults) : base(token)
        {
            this.maxResults = maxResults;
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) => results.TakeWhile(_ => !token.IsCancellationRequested).Take(maxResults);
    }
}
