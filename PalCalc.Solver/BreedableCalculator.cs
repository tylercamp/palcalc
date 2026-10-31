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

    private readonly ILookup<Pal, PalInstance> ownedByPal = settings.OwnedPals.ToLookup(p => p.Pal);

    public BreedableResult Calculate()
    {
        // same-species pairs only ever produce the same species
        var candidates = settings.BreedingDB.Breeding
            .Where(b => b.Parent1.Pal != b.Parent2.Pal)
            .ToList();

        Dictionary<Pal, int> tiers = [];
        Dictionary<Pal, PalGender> genders = [];
        Dictionary<Pal, BreedingResult> sources = [];
        Dictionary<Pal, (PalInstance, PalInstance)> parents = [];
        Dictionary<Pal, IV_Set> ivs = [];

        foreach (var pal in settings.OwnedPals)
        {
            tiers[pal.Pal] = 0;
            genders[pal.Pal] = genders.GetValueOrDefault(pal.Pal) | pal.Gender;
        }

        foreach (var group in ownedByPal)
            ivs[group.Key] = ExactIVs(group.OrderByDescending(TotalIVs).ThenBy(p => p.InstanceId, StringComparer.Ordinal).First());

        // each generation only uses what was available at the end of the previous generation. a
        // generation can also add nothing but a new gender for an owned species, so tiers can be empty
        for (var tier = 1; ; tier++)
        {
            Dictionary<Pal, BreedingResult> newSources = [];
            HashSet<Pal> newGenders = [];

            foreach (var breeding in candidates)
            {
                if (!tiers.ContainsKey(breeding.Parent1.Pal) || !tiers.ContainsKey(breeding.Parent2.Pal)) continue;
                if (!ParentGenders.Any(g => CanPairAs(breeding, g, genders))) continue;

                if (!tiers.ContainsKey(breeding.Child))
                {
                    if (!newSources.TryGetValue(breeding.Child, out var current) || IsPreferred(breeding, current, tiers))
                        newSources[breeding.Child] = breeding;
                }
                else if (genders[breeding.Child] != PalGender.WILDCARD)
                {
                    newGenders.Add(breeding.Child);
                }
            }

            if (newSources.Count == 0 && newGenders.Count == 0) break;

            // (must use the genders from this generation, not the genders which become available later)
            foreach (var (pal, source) in newSources)
            {
                parents[pal] = BestOwnedParents(source, genders);
                ivs[pal] = InheritedIVs(source, parents[pal], ivs);
            }

            foreach (var (pal, source) in newSources)
            {
                tiers[pal] = tier;
                genders[pal] = PalGender.WILDCARD;
                sources[pal] = source;
            }

            foreach (var pal in newGenders)
                genders[pal] = PalGender.WILDCARD;
        }

        return new(
            tiers
                .Select(kvp => new BreedableEntry(
                    kvp.Key,
                    kvp.Value,
                    sources.GetValueOrDefault(kvp.Key),
                    parents.GetValueOrDefault(kvp.Key).Item1,
                    parents.GetValueOrDefault(kvp.Key).Item2,
                    ivs.GetValueOrDefault(kvp.Key)
                ))
                .OrderBy(e => e.Tier)
                .ThenBy(e => e.Pal.InternalIndex),
            settings.BreedingDB.Breeding
                .Select(b => b.Child)
                .Distinct()
                .Where(p => !tiers.ContainsKey(p))
                .OrderBy(p => p.InternalIndex)
        );
    }

    // an owned species only has the genders which are actually owned, until it can be bred for the other gender
    private bool CanPairAs(BreedingResult breeding, PalGender parent1Gender, Dictionary<Pal, PalGender> genders)
    {
        var parent1 = breeding.Parent1.Pal;
        var parent2 = breeding.Parent2.Pal;
        var parent2Gender = parent1Gender.OppositeGender();

        var hasGenders = settings.UseGenderReversers || (
            genders[parent1].HasFlag(parent1Gender) &&
            genders[parent2].HasFlag(parent2Gender)
        );

        return hasGenders && breeding.Matches(parent1, parent1Gender, parent2, parent2Gender);
    }

    private (PalInstance, PalInstance) BestOwnedParents(BreedingResult breeding, Dictionary<Pal, PalGender> genders)
    {
        PalInstance BestOwned(Pal pal, PalGender gender) =>
            ownedByPal[pal]
                .Where(p => settings.UseGenderReversers || p.Gender.HasFlag(gender))
                .OrderByDescending(p => p.Gender.HasFlag(gender))
                .ThenByDescending(TotalIVs)
                .ThenBy(p => p.InstanceId, StringComparer.Ordinal)
                .FirstOrDefault();

        // prefer the parents which need the least work, a parent which needs to be bred has no owned instance
        return ParentGenders
            .Where(g => CanPairAs(breeding, g, genders))
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

    private static IV_Set ExactIVs(PalInstance pal) =>
        new(
            HP: new(IsRelevant: true, Min: pal.IV_HP, Max: pal.IV_HP),
            Attack: new(IsRelevant: true, Min: pal.IV_Attack, Max: pal.IV_Attack),
            Defense: new(IsRelevant: true, Min: pal.IV_Defense, Max: pal.IV_Defense)
        );

    private static int TotalIVs(PalInstance pal) =>
        pal == null ? 0 : pal.IV_HP + pal.IV_Attack + pal.IV_Defense;

    // prefers the parents with the lowest tiers, using species order to keep the result stable between runs
    private static bool IsPreferred(BreedingResult breeding, BreedingResult current, Dictionary<Pal, int> tiers) =>
        SourceOrder(breeding, tiers).CompareTo(SourceOrder(current, tiers)) < 0;

    private static (int, int, int) SourceOrder(BreedingResult breeding, Dictionary<Pal, int> tiers)
    {
        var index1 = breeding.Parent1.Pal.InternalIndex;
        var index2 = breeding.Parent2.Pal.InternalIndex;

        return (
            tiers[breeding.Parent1.Pal] + tiers[breeding.Parent2.Pal],
            Math.Min(index1, index2),
            Math.Max(index1, index2)
        );
    }
}
