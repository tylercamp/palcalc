using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
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
    PalBreedingDB breedingDB,
    AttackTargetContext attackTargets
)
{
    public List<IPalReference> Build(PalSpecifier target)
    {
        // This step selects concrete owned instances and creates composite
        // gender candidates before the frontier simplifies breeding paths.
        // Those choices cannot be recovered from effective properties alone.
        InitialOwnedState StateWithoutGender(OwnedPalReference reference) =>
            new(
                reference.Pal,
                new PassiveSetKey(
                    reference.ActualPassives
                        .Intersect(reference.EffectivePassives)
                        .ToList()
                ),
                new RelevantIVKey(reference.IVs),
                reference.AttackProfile,
                reference.HasNeutralAttack
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
            {
                var masteredAttacks = p.ActiveSkills ?? [];
                return new OwnedPalReference(
                    instance: p,
                    effectivePassives: p.PassiveSkills.ToDedicatedPassives(target.DesiredPassives),
                    effectiveIVs: new IV_Set
                    {
                        HP = MakeIV(target.IV_HP, p.IV_HP),
                        Attack = MakeIV(target.IV_Attack, p.IV_Attack),
                        Defense = MakeIV(target.IV_Defense, p.IV_Defense),
                    },
                    attackProfile: attackTargets.IsActive
                        ? new(new AttackProfileEntry(attackTargets.MaskOf(masteredAttacks), 0, TimeSpan.Zero, 0, false))
                        : AttackProfile.Inactive,
                    hasNeutralAttack: attackTargets.IsActive && masteredAttacks.Any(attack => !attack.CanInherit)
                );
            })
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

        if (!male.AttackProfile.Equals(female.AttackProfile) ||
            male.HasNeutralAttack != female.HasNeutralAttack)
            return [male, female];

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
                // we're being asked to find a breeding path to the target pal, don't consider catching a wild one
                .Where(p => p != target.Pal)
                .Where(withinBreedingSteps)
                .SelectMany(p =>
                {
                    var guaranteedPassives = p.GuaranteedPassiveSkills(settings.DB);
                    var attackState = attackTargets.StateOf(p);
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
                                mechanics,
                                attackProfile: attackTargets.IsActive
                                    ? new(new AttackProfileEntry(
                                        attackState.Level1TargetMask,
                                        0,
                                        mechanics.TimeToCatch(p) / mechanics.PassivesWildAtMostN[numRandomPassives],
                                        0,
                                        false
                                    ))
                                    : AttackProfile.Inactive,
                                hasNeutralAttack: attackTargets.IsActive && attackState.HasNeutralLevel1Attack
                            )
                        );
                })
                .Where(candidate => candidate.BreedingEffort <= settings.MaxEffort)
        );
    }

    private readonly record struct InitialOwnedState(
        Pal Pal,
        PassiveSetKey Passives,
        RelevantIVKey IVs,
        AttackProfile AttackProfile,
        bool HasNeutralAttack
    );
}
