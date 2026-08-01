using PalCalc.Solver.PalReference;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.ResultPruning
{
    public sealed class CachedResultData(IEnumerable<IPalReference> results)
    {
        public Dictionary<IPalReference, List<IPalReference>> InnerReferences { get; } =
            results.ToDictionary(result => result, result => result.AllReferences().ToList());
    }

    public abstract class ResultPruningRule
    {
        protected readonly CancellationToken token;

        protected ResultPruningRule(CancellationToken token)
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

        public abstract class ForceDeterministic : ResultPruningRule
        {
            protected ForceDeterministic(CancellationToken token) : base(token)
            {
            }

            public sealed override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
                ApplyNonDeterministic(results.OrderBy(result => result.GetHashCode()), cachedData);

            protected abstract IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData);
        }
    }
}
