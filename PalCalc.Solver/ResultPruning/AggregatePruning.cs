using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.ResultPruning
{
    internal sealed class AggregatePruning : ResultPruningRule
    {
        private readonly IReadOnlyList<ResultPruningRule> contents;

        public AggregatePruning(CancellationToken token, IEnumerable<ResultPruningRule> contents) : base(token)
        {
            this.contents = contents.ToList();
        }

        public override IEnumerable<IPalReference> Apply(
            IEnumerable<IPalReference> results,
            CachedResultData cachedData
        ) =>
            contents.Aggregate(
                results,
                (retained, rule) => rule.Apply(retained, cachedData)
            );
    }
}
