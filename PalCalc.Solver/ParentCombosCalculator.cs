using PalCalc.Model;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver;

/// <summary>
/// Finds every pair of parent species which breeds one child, and which of the two
/// parents are owned. A parent which isn't owned says whether it can be bred from the
/// owned pals, using `BreedableCalculator`, or has to be caught. Attacks are ignored,
/// and so are passives, except that Philanthropist can be preferred when picking the
/// owned pals.
/// </summary>
public sealed class ParentCombosCalculator(BreedingSolverSettings settings)
{
    private static readonly PalGender[] ParentGenders = [PalGender.MALE, PalGender.FEMALE];

    private readonly ILookup<Pal, PalInstance> ownedByPal = settings.OwnedPals.ToLookup(p => p.Pal);

    // the flower gumoss breeds the same children as the normal gumoss, and has the same name and icon, so its
    // pairs would look like duplicates. they're only kept when one is owned, same as `BreedableCalculator`
    private readonly bool showsHiddenPal = settings.OwnedPals.Any(p => p.Pal.InternalName == BreedableCalculator.HiddenPalInternalName);

    // species which only ever come from a pair of themselves
    private readonly HashSet<Pal> catchOnly = settings.BreedingDB.Breeding
        .GroupBy(b => b.Child)
        .Where(g => g.All(b => b.Parent1.Pal == g.Key && b.Parent2.Pal == g.Key))
        .Select(g => g.Key)
        .ToHashSet();

    /// <summary>
    /// Whether the pal has a passive which speeds up breeding, i.e. Philanthropist. Uses the
    /// same check as the solver's breeding time (`ToTimeFactor`).
    /// </summary>
    public static bool HasBreedSpeed(PalInstance instance) => instance?.PassiveSkills?.ToTimeFactor() < 1;

    public ParentCombosResult Calculate(Pal child, bool preferBreedSpeed = false) =>
        Calculate(child, new BreedableCalculator(settings).Calculate(), preferBreedSpeed);

    // (takes the breedable result so it doesn't need recalculating for each child)
    //
    // `preferBreedSpeed` picks owned pals with Philanthropist ahead of higher IVs, after the gender needed
    public ParentCombosResult Calculate(Pal child, BreedableResult breedable, bool preferBreedSpeed = false)
    {
        var entries = breedable.Entries.ToDictionary(e => e.Pal);

        return new(
            child,
            settings.BreedingDB.Breeding
                .Where(b => b.Child == child)
                .Where(b => showsHiddenPal || b.Parents.All(p => p.Pal.InternalName != BreedableCalculator.HiddenPalInternalName))
                .Select(b => ToCombo(b, entries, preferBreedSpeed))
                .OrderByDescending(c => c.NumOwnedParents)
                .ThenBy(c => Math.Min(c.Source.Parent1.Pal.InternalIndex, c.Source.Parent2.Pal.InternalIndex))
                .ThenBy(c => Math.Max(c.Source.Parent1.Pal.InternalIndex, c.Source.Parent2.Pal.InternalIndex))
        );
    }

    // uses the gender order with the most owned parents, then the fewest gender reversers, then (when preferred)
    // the most with Philanthropist, then the most which can be bred, then the best IVs
    private ParentCombo ToCombo(BreedingResult breeding, Dictionary<Pal, BreedableEntry> entries, bool preferBreedSpeed)
    {
        var parent1 = breeding.Parent1.Pal;
        var parent2 = breeding.Parent2.Pal;

        return ParentGenders
            .Where(g => breeding.Matches(parent1, g, parent2, g.OppositeGender()))
            .Select(g =>
            {
                var instance1 = BestOwned(parent1, g, null, preferBreedSpeed);
                // (a pal can't be bred with itself, so a pair of the same species needs two of them)
                var instance2 = BestOwned(parent2, g.OppositeGender(), parent1 == parent2 ? instance1 : null, preferBreedSpeed);

                return new ParentCombo(
                    breeding,
                    g,
                    instance1,
                    instance2,
                    Availability(parent1, instance1, entries),
                    Availability(parent2, instance2, entries),
                    instance1 == null || instance2 == null ? null : MergedIVs(instance1, instance2)
                );
            })
            .OrderByDescending(c => c.NumOwnedParents)
            .ThenBy(c =>
                (c.Parent1Instance?.Gender.HasFlag(c.Parent1Gender) == false ? 1 : 0) +
                (c.Parent2Instance?.Gender.HasFlag(c.Parent1Gender.OppositeGender()) == false ? 1 : 0)
            )
            .ThenByDescending(c => preferBreedSpeed ? c.NumBreedSpeedParents : 0)
            .ThenByDescending(c =>
                (c.Parent1Availability == ParentAvailability.Breedable ? 1 : 0) +
                (c.Parent2Availability == ParentAvailability.Breedable ? 1 : 0)
            )
            .ThenByDescending(c => BreedableCalculator.TotalIVs(c.Parent1Instance) + BreedableCalculator.TotalIVs(c.Parent2Instance))
            .First();
    }

    private PalInstance BestOwned(Pal pal, PalGender gender, PalInstance exclude, bool preferBreedSpeed) =>
        BreedableCalculator.BestOwned(
            ownedByPal[pal].Where(p => p != exclude),
            gender,
            settings.UseGenderReversers,
            preferBreedSpeed ? HasBreedSpeed : null
        );

    // a parent which isn't owned can be bred once it's available in either gender, which also covers the
    // gender that isn't owned, and a second pal of a species where only one is owned
    private ParentAvailability Availability(Pal pal, PalInstance instance, Dictionary<Pal, BreedableEntry> entries)
    {
        if (instance != null) return ParentAvailability.Owned;
        if (entries.GetValueOrDefault(pal)?.BothGendersAvailable == true) return ParentAvailability.Breedable;

        return catchOnly.Contains(pal) ? ParentAvailability.CatchOnly : ParentAvailability.NotBreedable;
    }

    private static IV_Set MergedIVs(PalInstance a, PalInstance b)
    {
        var ivs1 = BreedableCalculator.ExactIVs(a);
        var ivs2 = BreedableCalculator.ExactIVs(b);

        return new(
            HP: IV_Value.Merge(ivs1.HP, ivs2.HP),
            Attack: IV_Value.Merge(ivs1.Attack, ivs2.Attack),
            Defense: IV_Value.Merge(ivs1.Defense, ivs2.Defense)
        );
    }
}
