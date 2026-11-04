using PalCalc.Model;
using PalCalc.Solver.PalReference.Properties;
using System.Collections.ObjectModel;

namespace PalCalc.Solver;

/// <summary>
/// How a parent in a `ParentCombo` can be had.
/// </summary>
public enum ParentAvailability
{
    // owned, in the gender needed or with gender reversers
    Owned,
    // can be bred from the owned pals, in the gender needed
    Breedable,
    // only ever bred from itself, so one has to be caught first
    CatchOnly,
    // can be bred from other species, but not from the owned pals
    NotBreedable,
}

/// <summary>
/// One pair of parent species which breeds the child.
///
/// `Parent1Gender` is the gender `Source.Parent1` needs, and `Source.Parent2` needs the
/// opposite one. `Parent1Instance` and `Parent2Instance` are the owned pals to use, and
/// are `null` when that parent isn't owned. `IVs` is the range the child can have, and
/// is only known when both parents are owned.
/// </summary>
public sealed record ParentCombo(
    BreedingResult Source,
    PalGender Parent1Gender,
    PalInstance Parent1Instance,
    PalInstance Parent2Instance,
    ParentAvailability Parent1Availability,
    ParentAvailability Parent2Availability,
    IV_Set? IVs
)
{
    public int NumOwnedParents =>
        (Parent1Availability == ParentAvailability.Owned ? 1 : 0) +
        (Parent2Availability == ParentAvailability.Owned ? 1 : 0);
}

/// <summary>
/// The completed output of one parent combos calculation.
/// </summary>
public sealed class ParentCombosResult
{
    public ParentCombosResult(Pal child, IEnumerable<ParentCombo> combos)
    {
        Child = child;
        Combos = new ReadOnlyCollection<ParentCombo>([.. combos]);
    }

    public Pal Child { get; }

    /// <summary>
    /// Every pair which breeds the child, with the most owned parents first.
    /// </summary>
    public IReadOnlyList<ParentCombo> Combos { get; }
}
