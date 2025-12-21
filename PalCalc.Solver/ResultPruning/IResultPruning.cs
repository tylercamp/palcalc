using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class CachedResultData(IEnumerable<IPalReference> results)
    {
        public Dictionary<IPalReference, List<IPalReference>> InnerReferences { get; } = results.ToDictionary(r => r, r => r.AllReferences().ToList());
    }

    public abstract class IResultPruning
    {
        protected CancellationToken token;
        public IResultPruning(CancellationToken token)
        {
            this.token = token;
        }

        public abstract IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData);

        protected IEnumerable<IPalReference> MinGroupOf<T>(IEnumerable<IPalReference> input, Func<IPalReference, T> grouping)
        {
            var comp = Comparer<T>.Default;
            try
            {
                if (token.IsCancellationRequested)
                    return [];

                var res = new List<IPalReference>();
                var minEval = default(T);

                foreach (var r in input)
                {
                    if (token.IsCancellationRequested)
                        return [];

                    if (res.Count == 0)
                    {
                        res.Add(r);
                        minEval = grouping(r);
                    }
                    else
                    {
                        var eval = grouping(r);
                        var comparison = comp.Compare(eval, minEval);
                        if (comparison < 0)
                        {
                            res.Clear();
                            res.Add(r);
                            minEval = eval;
                        }
                        else if (comparison == 0)
                        {
                            res.Add(r);
                        }
                    }
                }

                return res;
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                return input;
            }
        }

        public abstract class ForceDeterministic : IResultPruning
        {
            protected ForceDeterministic(CancellationToken token) : base(token)
            {
            }

            public sealed override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
                ApplyNonDeterministic(results.OrderBy(r => r.GetHashCode()), cachedData);

            protected abstract IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData);
        }
    }
}
