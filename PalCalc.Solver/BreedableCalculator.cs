using PalCalc.Model;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver;

/// <summary>
/// Finds every species which can be reached from the owned pals in `settings`,
/// grouped by the number of breeding generations needed to reach them. Passives,
/// IVs, and attacks are ignored.
/// </summary>
public sealed class BreedableCalculator(BreedingSolverSettings settings)
{
    private static readonly PalGender[] ParentGenders = [PalGender.MALE, PalGender.FEMALE];

    // the flower gumoss has the same name and icon as the normal gumoss, so it would look like a
    // duplicate. it doesn't get an entry of its own, but can still be used as a parent if owned
    public const string HiddenPalInternalName = "PlantSlime_Flower";

    private readonly ILookup<Pal, PalInstance> ownedByPal = settings.OwnedPals.ToLookup(p => p.Pal);

    public BreedableResult Calculate()
    {
        // same-species pairs only ever produce the same species
        var candidates = ownedByPal.Count < 2
            ? []
            : settings.BreedingDB.Breeding.Where(b => b.Parent1.Pal != b.Parent2.Pal).ToList();

        Dictionary<Pal, (int Tier, PalGender Genders)> available = [];
        Dictionary<Pal, BreedingResult> sources = [];
        Dictionary<Pal, (PalInstance, PalInstance)> parents = [];
        Dictionary<Pal, PalInstance> bestOwned = [];
        Dictionary<Pal, IV_Set> ivs = [];

        foreach (var pal in settings.OwnedPals)
        {
            available[pal.Pal] = (0, available.GetValueOrDefault(pal.Pal).Genders | pal.Gender);
        }

        foreach (var group in ownedByPal)
        {
            var best = group
                .OrderByDescending(TotalIVs)
                .ThenBy(p => p.Location?.ContainerId, StringComparer.Ordinal)
                .ThenBy(p => p.Location?.Index)
                .First();

            bestOwned[group.Key] = best;
            ivs[group.Key] = ExactIVs(best);
        }

        // each generation only uses what was available at the end of the previous generation. a
        // generation can also add nothing but a new gender for an owned species, so tiers can be empty
        for (var tier = 1; ; tier++)
        {
            Dictionary<Pal, (BreedingResult Source, (int TierSum, int FirstIndex, int SecondIndex) Rank)> newSources = [];
            HashSet<Pal> newGenders = [];

            foreach (var breeding in candidates)
            {
                if (!available.TryGetValue(breeding.Parent1.Pal, out var parent1) ||
                    !available.TryGetValue(breeding.Parent2.Pal, out var parent2)) continue;

                var childIsAvailable = available.TryGetValue(breeding.Child, out var child);
                if (childIsAvailable && (child.Genders == PalGender.WILDCARD || newGenders.Contains(breeding.Child))) continue;

                if (!CanPairAs(breeding, PalGender.MALE, parent1.Genders, parent2.Genders) &&
                    !CanPairAs(breeding, PalGender.FEMALE, parent1.Genders, parent2.Genders)) continue;

                if (!childIsAvailable)
                {
                    var rank = SourceOrder(breeding, parent1.Tier + parent2.Tier);
                    if (!newSources.TryGetValue(breeding.Child, out var current) || rank.CompareTo(current.Rank) < 0)
                        newSources[breeding.Child] = (breeding, rank);
                }
                else
                {
                    newGenders.Add(breeding.Child);
                }
            }

            if (newSources.Count == 0 && newGenders.Count == 0) break;

            // (must use the genders from this generation, not the genders which become available later)
            foreach (var (pal, candidate) in newSources)
            {
                var source = candidate.Source;
                var ownedParents = BestOwnedParents(source, available[source.Parent1.Pal].Genders, available[source.Parent2.Pal].Genders);
                parents[pal] = ownedParents;
                ivs[pal] = InheritedIVs(source, ownedParents, ivs);
            }

            foreach (var (pal, candidate) in newSources)
            {
                available[pal] = (tier, PalGender.WILDCARD);
                sources[pal] = candidate.Source;
            }

            foreach (var pal in newGenders)
                available[pal] = (available[pal].Tier, PalGender.WILDCARD);
        }

        return new(
            available
                .Where(kvp => kvp.Key.InternalName != HiddenPalInternalName)
                .Select(kvp => new BreedableEntry(
                    kvp.Key,
                    kvp.Value.Tier,
                    sources.GetValueOrDefault(kvp.Key),
                    parents.GetValueOrDefault(kvp.Key).Item1,
                    parents.GetValueOrDefault(kvp.Key).Item2,
                    bestOwned.GetValueOrDefault(kvp.Key),
                    ivs.GetValueOrDefault(kvp.Key),
                    kvp.Value.Genders.HasFlag(PalGender.WILDCARD)
                ))
                .OrderBy(e => e.Tier)
                .ThenBy(e => e.Pal.InternalIndex),
            settings.DB.Pals
                .Where(p => !available.ContainsKey(p) && p.InternalName != HiddenPalInternalName)
                .OrderBy(p => p.InternalIndex)
        );
    }

    // an owned species only has the genders which are actually owned, until it can be bred for the other gender
    private bool CanPairAs(BreedingResult breeding, PalGender parent1Gender, PalGender parent1Available, PalGender parent2Available)
    {
        var parent1 = breeding.Parent1.Pal;
        var parent2 = breeding.Parent2.Pal;
        var parent2Gender = parent1Gender.OppositeGender();

        var hasGenders = settings.UseGenderReversers || (
            parent1Available.HasFlag(parent1Gender) &&
            parent2Available.HasFlag(parent2Gender)
        );

        return hasGenders && breeding.Matches(parent1, parent1Gender, parent2, parent2Gender);
    }

    // the owned pal to use as a parent with the given gender, or `null` if it needs to be bred instead
    // `preferred` picks those pals ahead of higher IVs, after the gender needed
    internal static PalInstance BestOwned(IEnumerable<PalInstance> owned, PalGender gender, bool useGenderReversers, Func<PalInstance, bool> preferred = null) =>
        owned
            .Where(p => useGenderReversers || p.Gender.HasFlag(gender))
            .OrderByDescending(p => p.Gender.HasFlag(gender))
            .ThenByDescending(p => preferred?.Invoke(p) == true)
            .ThenByDescending(TotalIVs)
            .ThenBy(p => p.Location?.ContainerId, StringComparer.Ordinal)
            .ThenBy(p => p.Location?.Index)
            .FirstOrDefault();

    private (PalInstance, PalInstance) BestOwnedParents(BreedingResult breeding, PalGender parent1Available, PalGender parent2Available)
    {
        PalInstance BestOwned(Pal pal, PalGender gender) =>
            BreedableCalculator.BestOwned(ownedByPal[pal], gender, settings.UseGenderReversers);

        // prefer the parents which need the least work, a parent which needs to be bred has no owned instance
        return ParentGenders
            .Where(g => CanPairAs(breeding, g, parent1Available, parent2Available))
            .Select(g => (
                Gender: g,
                Parent1: BestOwned(breeding.Parent1.Pal, g),
                Parent2: BestOwned(breeding.Parent2.Pal, g.OppositeGender())
            ))
            .OrderByDescending(p =>
                (p.Parent1?.Gender.HasFlag(p.Gender) == true ? 1 : 0) +
                (p.Parent2?.Gender.HasFlag(p.Gender.OppositeGender()) == true ? 1 : 0)
            )
            .ThenByDescending(p => TotalIVs(p.Parent1) + TotalIVs(p.Parent2))
            .Select(p => (p.Parent1, p.Parent2))
            .FirstOrDefault();
    }

    // an owned pal's IVs are all known, so these only ever need `IV_Value.Merge` rather than the
    // handling of random and irrelevant IVs in `CandidateExpander`
    private static IV_Set InheritedIVs(
        BreedingResult breeding,
        (PalInstance, PalInstance) parents,
        Dictionary<Pal, IV_Set> ivs
    )
    {
        var parent1 = parents.Item1 == null ? ivs[breeding.Parent1.Pal] : ExactIVs(parents.Item1);
        var parent2 = parents.Item2 == null ? ivs[breeding.Parent2.Pal] : ExactIVs(parents.Item2);

        return new(
            HP: IV_Value.Merge(parent1.HP, parent2.HP),
            Attack: IV_Value.Merge(parent1.Attack, parent2.Attack),
            Defense: IV_Value.Merge(parent1.Defense, parent2.Defense)
        );
    }

    public static IV_Set ExactIVs(PalInstance pal) =>
        new(
            HP: new(IsRelevant: true, Min: pal.IV_HP, Max: pal.IV_HP),
            Attack: new(IsRelevant: true, Min: pal.IV_Attack, Max: pal.IV_Attack),
            Defense: new(IsRelevant: true, Min: pal.IV_Defense, Max: pal.IV_Defense)
        );

    internal static int TotalIVs(PalInstance pal) =>
        pal == null ? 0 : pal.IV_HP + pal.IV_Attack + pal.IV_Defense;

    // prefers the parents with the lowest tiers, using species order to keep the result stable between runs
    private static (int, int, int) SourceOrder(BreedingResult breeding, int tierSum)
    {
        var index1 = breeding.Parent1.Pal.InternalIndex;
        var index2 = breeding.Parent2.Pal.InternalIndex;

        return (
            tierSum,
            Math.Min(index1, index2),
            Math.Max(index1, index2)
        );
    }
}
