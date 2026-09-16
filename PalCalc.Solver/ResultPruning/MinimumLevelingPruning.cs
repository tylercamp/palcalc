using PalCalc.Solver.PalReference;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.ResultPruning;

public sealed class MinimumLevelingPruning(CancellationToken token) : ResultPruningRule(token)
{
    internal static (int Pals, int Levels) TrainingOf(IEnumerable<IPalReference> references)
    {
        var trained = references.Distinct().Where(p => p.LevelRequirements is not null).ToArray();
        return (trained.Length, trained.Sum(p => p.LevelRequirements.FinalLevel - p.LevelRequirements.InitialLevel));
    }

    internal static (int Pals, int Levels) TrainingOf(IPalReference reference) =>
        TrainingOf(reference.AllReferences());

    public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
        MinGroupOf(results, r => TrainingOf(cachedData.InnerReferences[r]));
}
