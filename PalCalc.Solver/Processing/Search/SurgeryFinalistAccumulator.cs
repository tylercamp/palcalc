using System.Collections.Concurrent;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// Retains candidates which can become terminal after the single post-search
/// surgery pass, before ordinary frontier pruning discards alternative paths.
/// </summary>
internal sealed class SurgeryFinalistAccumulator
{
    private readonly PalSpecifier prerequisiteTarget;
    private readonly PassiveSkill[] desiredSurgeryPassives;
    private readonly PassiveSkill[] requiredSurgeryPassives;
    private readonly AttackTargetContext attackTargets;
    private readonly BreedingSolverSettings settings;
    private readonly PalGender requiredGender;
    private readonly ConcurrentDictionary<IPalReference, byte> candidates = new();

    private SurgeryFinalistAccumulator(
        PalSpecifier target,
        BreedingSolverSettings settings,
        AttackTargetContext attackTargets,
        PassiveSkill[] desiredSurgeryPassives
    )
    {
        this.settings = settings;
        this.attackTargets = attackTargets;
        requiredGender = target.RequiredGender;
        this.desiredSurgeryPassives = desiredSurgeryPassives;
        requiredSurgeryPassives = target.RequiredPassives
            .Where(desiredSurgeryPassives.Contains)
            .ToArray();

        prerequisiteTarget = target.NormalizedCopy();
        prerequisiteTarget.RequiredPassives = prerequisiteTarget.RequiredPassives
            .Except(desiredSurgeryPassives)
            .ToList();
        // Gender reversers are handled by finalization, not surgery.
        prerequisiteTarget.RequiredGender = PalGender.WILDCARD;
    }

    public static SurgeryFinalistAccumulator Create(
        PalSpecifier target,
        BreedingSolverSettings settings,
        AttackTargetContext attackTargets
    )
    {
        var desiredSurgeryPassives = target.DesiredPassives
            .Where(passive => passive.SupportsSurgery)
            .Where(settings.SurgeryPassives.Contains)
            .ToArray();
        return desiredSurgeryPassives.Length == 0
            ? null
            : new(target, settings, attackTargets, desiredSurgeryPassives);
    }

    public IReadOnlyList<IPalReference> Candidates => candidates.Keys.ToArray();

    public void Observe(IEnumerable<IPalReference> references)
    {
        foreach (var reference in references)
            TryRetain(reference);
    }

    public bool TryRetain(IPalReference candidate)
    {
        if (
            candidate.Pal != prerequisiteTarget.Pal ||
            attackTargets?.IsActive == true &&
                !candidate.AttackProfile.Contains(attackTargets.FullTargetMask) ||
            !CanRetain(
            candidate.Pal,
            candidate.Gender,
            candidate.IVs,
            candidate.EffectivePassives,
            candidate.TotalCost
        ))
            return false;

        return candidates.TryAdd(candidate, 0);
    }

    public bool TryRetain(ref CandidateDraft candidate)
    {
        if (
            candidate.Pal != prerequisiteTarget.Pal ||
            attackTargets?.IsActive == true &&
                !candidate.AttackProfile.Contains(attackTargets.FullTargetMask) ||
            !CanRetain(
            candidate.Pal,
            candidate.Gender,
            candidate.IVs,
            candidate.EffectivePassives,
            candidate.TotalCost
        ))
            return false;

        return candidates.TryAdd(candidate.Materialize(), 0);
    }

    private bool CanRetain(
        Pal pal,
        PalGender gender,
        IV_Set ivs,
        List<PassiveSkill> passives,
        int totalCost
    )
    {
        if (!prerequisiteTarget.IsSatisfiedByIgnoringAttacks(
            pal,
            gender,
            ivs,
            passives
        ))
            return false;

        if (
            requiredGender != PalGender.WILDCARD &&
            gender != PalGender.WILDCARD &&
            gender != requiredGender &&
            !settings.UseGenderReversers
        )
            return false;

        var missingDesiredPassive = false;
        foreach (var passive in desiredSurgeryPassives)
            if (!passives.Contains(passive))
            {
                missingDesiredPassive = true;
                break;
            }
        if (!missingDesiredPassive)
            return false;

        foreach (var passive in requiredSurgeryPassives)
            if (!passives.Contains(passive))
            {
                totalCost += passive.SurgeryCost;
                if (totalCost > settings.MaxSurgeryCost)
                    return false;
            }

        return true;
    }
}
