using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver;

/// <summary>
/// Builds and reduces the owned and wild candidates which seed a solver run.
/// </summary>
internal sealed class InitialPalBuilder(BreedingSolverSettings settings)
{
    private readonly PalBreedingDB breedingDB =
        PalBreedingDB.LoadEmbedded(settings.DB);

    public List<IPalReference> Build(PalSpecifier target)
    {
        // This reduction remains before the frontier because it selects concrete
        // owned instances and constructs composite-gender references. Those
        // choices cannot be recovered from frontier state keys alone.
        var allPropertiesGroupFn = PalProperty.Combine(
            PalProperty.Pal,
            PalProperty.RelevantPassives,
            PalProperty.IvRelevance,
            PalProperty.Gender
        );
        var allExceptGenderGroupFn = PalProperty.Combine(
            PalProperty.Pal,
            PalProperty.RelevantPassives,
            PalProperty.IvRelevance
        );

        bool WithinBreedingSteps(Pal pal) =>
            breedingDB.MinBreedingSteps[pal][target.Pal] <=
            settings.MaxBreedingSteps;

        static IV_Value MakeIV(int minValue, int value) =>
            new(
                IsRelevant: minValue != 0 && value >= minValue,
                Min: value,
                Max: value
            );

        var initialContent = settings.OwnedPals
            .Where(p => WithinBreedingSteps(p.Pal))
            .Where(p =>
                p.PassiveSkills
                    .Except(target.DesiredPassives)
                    .Count() <=
                settings.MaxInputIrrelevantPassives
            )
            .Select(p =>
                new OwnedPalReference(
                    instance: p,
                    effectivePassives:
                        p.PassiveSkills.ToDedicatedPassives(
                            target.DesiredPassives
                        ),
                    effectiveIVs: new IV_Set
                    {
                        HP = MakeIV(target.IV_HP, p.IV_HP),
                        Attack = MakeIV(
                            target.IV_Attack,
                            p.IV_Attack
                        ),
                        Defense = MakeIV(
                            target.IV_Defense,
                            p.IV_Defense
                        ),
                    }
                )
            )
            .GroupBy(pal => allPropertiesGroupFn(pal))
            .Select(group =>
                group
                    .OrderBy(p => p.ActualPassives.Count)
                    .ThenBy(p =>
                        PreferredLocationPruning.LocationOrderingOf(
                            p.UnderlyingInstance.Location.Type
                        )
                    )
                    .ThenByDescending(p =>
                        p.UnderlyingInstance.IV_HP +
                        p.UnderlyingInstance.IV_Attack +
                        p.UnderlyingInstance.IV_Defense
                    )
                    .First()
            )
            .GroupBy(pal => allExceptGenderGroupFn(pal))
            .Select(group => group.ToList())
            .SelectMany<
                List<OwnedPalReference>,
                IPalReference
            >(CombineGenders)
            .ToList();

        if (settings.MaxWildPals > 0)
            AddWildCandidates(initialContent, target, WithinBreedingSteps);

        return initialContent;
    }

    private static IEnumerable<IPalReference> CombineGenders(
        List<OwnedPalReference> group
    )
    {
        if (group.Count == 1)
            return group;
        if (group.Count != 2)
            throw new NotImplementedException();

        var male = group.SingleOrDefault(
            pal => pal.Gender == PalGender.MALE
        );
        var female = group.SingleOrDefault(
            pal => pal.Gender == PalGender.FEMALE
        );
        var composite = new CompositeOwnedPalReference(male, female);

        return male.EffectivePassivesHash ==
            female.EffectivePassivesHash
            ? [composite]
            : [male, female, composite];
    }

    private void AddWildCandidates(
        List<IPalReference> initialContent,
        PalSpecifier target,
        Func<Pal, bool> withinBreedingSteps
    )
    {
        initialContent.AddRange(
            settings.AllowedWildPals
                .Where(p =>
                    !settings.OwnedPals.Any(
                        instance => instance.Pal == p
                    )
                )
                .Where(withinBreedingSteps)
                .SelectMany(p =>
                {
                    var guaranteedPassives =
                        p.GuaranteedPassiveSkills(settings.DB);
                    var numIrrelevantGuaranteed =
                        guaranteedPassives
                            .Except(target.DesiredPassives)
                            .Count();

                    return Enumerable
                        .Range(
                            0,
                            Math.Clamp(
                                value:
                                    settings
                                        .MaxInputIrrelevantPassives -
                                    numIrrelevantGuaranteed,
                                min:
                                    numIrrelevantGuaranteed >
                                    settings
                                        .MaxInputIrrelevantPassives
                                        ? 0
                                        : 1,
                                max:
                                    GameConstants.MaxTotalPassives -
                                    guaranteedPassives.Count()
                            )
                        )
                        .Select(numRandomPassives =>
                            new WildPalReference(
                                p,
                                guaranteedPassives,
                                numRandomPassives
                            )
                        );
                })
                .Where(candidate =>
                    candidate.BreedingEffort <= settings.MaxEffort
                )
        );
    }
}
