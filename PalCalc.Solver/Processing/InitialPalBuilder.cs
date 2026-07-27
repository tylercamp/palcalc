using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing;

/// <summary>
/// Builds and reduces the owned and wild candidates which seed a solver run.
/// </summary>
internal sealed class InitialPalBuilder(
    BreedingSolverSettings settings,
    BreedingMechanics mechanics,
    PalBreedingDB breedingDB
)
{
    public List<IPalReference> Build(PalSpecifier target)
    {
        // This step selects concrete owned instances and creates composite
        // gender candidates before the frontier simplifies breeding paths.
        // Those choices cannot be recovered from effective properties alone.
        static (
            Pal Pal,
            PassiveSetKey Passives,
            RelevantIVKey IVs
        ) StateWithoutGender(OwnedPalReference reference) =>
            (
                reference.Pal,
                new PassiveSetKey(
                    reference.ActualPassives
                        .Intersect(reference.EffectivePassives)
                        .ToList()
                ),
                new RelevantIVKey(reference.IVs)
            );

        bool WithinBreedingSteps(Pal pal) =>
            breedingDB.MinBreedingSteps[pal][target.Pal] <= settings.MaxBreedingSteps;

        static IV_Value MakeIV(int minValue, int value) =>
            new(
                IsRelevant: minValue != 0 && value >= minValue,
                Min: value,
                Max: value
            );

        var initialContent = settings.OwnedPals
            .Where(p => WithinBreedingSteps(p.Pal))
            .Where(p =>
                p.PassiveSkills.Except(target.DesiredPassives).Count() <= settings.MaxInputIrrelevantPassives
            )
            .Select(p =>
                new OwnedPalReference(
                    instance: p,
                    effectivePassives: p.PassiveSkills.ToDedicatedPassives(target.DesiredPassives),
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
            .GroupBy(pal => (
                State: StateWithoutGender(pal),
                pal.Gender
            ))
            .Select(group =>
                group
                    .OrderBy(p => p.ActualPassives.Count)
                    .ThenBy(p =>
                        PreferredLocationPruning.LocationOrderingOf(p.UnderlyingInstance.Location.Type)
                    )
                    .ThenByDescending(p =>
                        p.UnderlyingInstance.IV_HP +
                        p.UnderlyingInstance.IV_Attack +
                        p.UnderlyingInstance.IV_Defense
                    )
                    .First()
            )
            .GroupBy(StateWithoutGender)
            .Select(group => group.ToList())
            .SelectMany(CombineGenders)
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

        var male = group.SingleOrDefault(pal => pal.Gender == PalGender.MALE);
        var female = group.SingleOrDefault(pal => pal.Gender == PalGender.FEMALE);
        var composite = new CompositeOwnedPalReference(male, female);

        return male.EffectivePassivesHash == female.EffectivePassivesHash
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
                .Where(p => !settings.OwnedPals.Any(instance => instance.Pal == p))
                .Where(withinBreedingSteps)
                .SelectMany(p =>
                {
                    var guaranteedPassives = p.GuaranteedPassiveSkills(settings.DB);
                    var numIrrelevantGuaranteed = guaranteedPassives.Except(target.DesiredPassives).Count();
                    var numAllowedRandomPassives = Math.Clamp(
                        value: settings.MaxInputIrrelevantPassives - numIrrelevantGuaranteed,
                        min: numIrrelevantGuaranteed > settings.MaxInputIrrelevantPassives ? 0 : 1,
                        max: GameConstants.MaxTotalPassives - guaranteedPassives.Count()
                    );

                    return Enumerable
                        .Range(0, numAllowedRandomPassives)
                        .Select(numRandomPassives =>
                            new WildPalReference(
                                p,
                                guaranteedPassives,
                                numRandomPassives,
                                mechanics
                            )
                        );
                })
                .Where(candidate => candidate.BreedingEffort <= settings.MaxEffort)
        );
    }
}
