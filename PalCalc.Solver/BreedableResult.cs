using PalCalc.Model;
using PalCalc.Solver.PalReference.Properties;
using System.Collections.ObjectModel;

namespace PalCalc.Solver;

/// <summary>
/// A species which can be reached from the owned pals. `Tier` is the number of
/// breeding generations needed, where owned species are tier 0. `Source` is one
/// breeding pair which produces the species, and is `null` for tier 0.
///
/// `Parent1Instance` and `Parent2Instance` are the owned pals to use for that pair,
/// preferring the required gender and then the highest IVs. Either can be `null` if
/// the parent needs to be bred instead.
///
/// `IVs` are the range of IVs available through `Source`, or the IVs of the best
/// owned pal for tier 0.
/// </summary>
public sealed record BreedableEntry(
    Pal Pal,
    int Tier,
    BreedingResult Source,
    PalInstance Parent1Instance,
    PalInstance Parent2Instance,
    IV_Set IVs
);

/// <summary>
/// The completed output of one breedable calculation.
/// </summary>
public sealed class BreedableResult
{
    public BreedableResult(
        IEnumerable<BreedableEntry> entries,
        IEnumerable<Pal> notReachable
    )
    {
        Entries = new ReadOnlyCollection<BreedableEntry>([.. entries]);
        NotReachable = new ReadOnlyCollection<Pal>([.. notReachable]);
    }

    /// <summary>
    /// Every species which can be reached, ordered by tier and then by species.
    /// </summary>
    public IReadOnlyList<BreedableEntry> Entries { get; }

    /// <summary>
    /// Every species which can't be reached, ordered by species.
    /// </summary>
    public IReadOnlyList<Pal> NotReachable { get; }
}
