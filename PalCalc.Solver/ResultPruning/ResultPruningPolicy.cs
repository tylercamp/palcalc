
namespace PalCalc.Solver.ResultPruning
{
    /// <summary>
    /// Creates the ordered, relatively expensive rule pipeline used to retain
    /// a small set of alternatives after quick breeding-effort comparisons.
    /// </summary>
    public sealed class ResultPruningPolicy
    {
        private readonly Func<CancellationToken, IEnumerable<ResultPruningRule>> createRules;

        public ResultPruningPolicy(Func<CancellationToken, IEnumerable<ResultPruningRule>> createRules)
        {
            ArgumentNullException.ThrowIfNull(createRules);
            this.createRules = createRules;
        }

        public ResultPruningRule Create(CancellationToken token) =>
            new AggregatePruning(token, createRules(token));

        public ResultPruningPolicy WithRule(Func<CancellationToken, ResultPruningRule> createRule)
        {
            ArgumentNullException.ThrowIfNull(createRule);
            return new(token => createRules(token).Append(createRule(token)));
        }

        public static ResultPruningPolicy Default { get; } = new(
            token => [
                new MinimumEffortPruning(token),
                new MinimumBreedingStepsPruning(token),
                new OptimalIVsPruning(token, maxIvDifference: 10),
                new MinimumCostPruning(token),
                new PreferredLocationPruning(token),
                new MinimumReusePruning(token),
                new MinimumWildPalsPruning(token),
                new MinimumReferencedPlayersPruning(token),
                new VariedResultsPruning(token, maxSimilarityPercent: 0.1f),
                new ResultLimitPruning(token, maxResults: 3),
            ]
        );
    }
}
