using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// Rebuilds one symbolic attack-profile path as a concrete breeding tree.
/// TODO: Generalize only if another result dimension needs its own realization.
/// </summary>
internal sealed class AttackResultMaterializer
{
    private readonly AttackTargetContext targets;
    private readonly BreedingSolverSettings settings;
    private readonly AttackProfileComposer composer;

    public AttackResultMaterializer(AttackTargetContext targets, BreedingSolverSettings settings)
    {
        this.targets = targets;
        this.settings = settings;
        composer = new AttackProfileComposer(targets, settings, new ObjectPoolFactory());
    }

    public IPalReference Materialize(IPalReference reference, AttackProfileEntry selectedEntry) =>
        reference switch
        {
            SurgeryTablePalReference surgery => new SurgeryTablePalReference(
                Materialize(surgery.Input, selectedEntry), surgery.Operations
            ),
            BredPalReference bred => MaterializeBred(bred, selectedEntry),
            _ => reference,
        };

    private BredPalReference MaterializeBred(
        BredPalReference bred,
        AttackProfileEntry selectedEntry
    )
    {
        var choice = composer
            .EnumerateChoices(
                bred.Pal,
                bred.Parent1,
                bred.Parent2,
                bred.PassivesProbability,
                bred.IVsProbability
            )
            .Where(choice => MaterializedEntryFor(bred, choice.ChildEntry).Equals(selectedEntry))
            .OrderBy(choice => choice.Mode)
            .ThenBy(choice => choice.Parent1TargetMask)
            .ThenBy(choice => choice.Parent2TargetMask)
            .ThenBy(choice => choice.Parent1Entry.MasteredTargetMask)
            .ThenBy(choice => choice.Parent2Entry.MasteredTargetMask)
            .ThenBy(choice => choice.Parent1Entry.TotalSpecialCakes)
            .ThenBy(choice => choice.Parent2Entry.TotalSpecialCakes)
            .FirstOrDefault();
        if (!MaterializedEntryFor(bred, choice.ChildEntry).Equals(selectedEntry))
            throw new InvalidOperationException("The selected attack profile entry cannot be reconstructed.");

        var parent1 = Materialize(bred.Parent1, choice.Parent1Entry);
        var parent2 = Materialize(bred.Parent2, choice.Parent2Entry);
        var inheritedAttacks = AttacksForMask((byte)(
            choice.Parent1TargetMask | choice.Parent2TargetMask
        ));
        var childMasteredAttacks = inheritedAttacks
            .Concat(bred.Pal.Level1ActiveSkills(settings.DB))
            .Distinct()
            .ToArray();
        var inheritance = new MaterializedAttackInheritance(
            (AttackInheritanceMode)choice.Mode,
            LoadoutFor(parent1, choice.Parent1TargetMask),
            LoadoutFor(parent2, choice.Parent2TargetMask),
            inheritedAttacks,
            childMasteredAttacks,
            choice.ChildEntry.SelfUsesSpecialCake ? choice.ChildEntry.SelfBreedings : 0,
            choice.AttackProbability
        );

        return new BredPalReference(
            settings.GameSettings,
            bred.Pal,
            parent1,
            parent2,
            [.. bred.EffectivePassives],
            bred.PassivesProbability,
            bred.IVs,
            bred.IVsProbability,
            attackProfile: new AttackProfile(bred.AttackProfile.HasNoopAttack, selectedEntry),
            materializedAttackInheritance: inheritance,
            avgRequiredBreedings: selectedEntry.SelfBreedings,
            gender: bred.Gender
        );
    }

    private ActiveSkill[] AttacksForMask(byte mask)
    {
        var attacks = new List<ActiveSkill>();
        for (var bit = (byte)1; bit != 0 && bit <= targets.FullTargetMask; bit <<= 1)
            if ((mask & bit) != 0)
                attacks.Add(targets.AttackForBit(bit));
        return attacks.ToArray();
    }

    private IReadOnlyList<ActiveSkill> LoadoutFor(IPalReference parent, byte targetMask)
    {
        var loadout = AttacksForMask(targetMask).ToList();
        if (loadout.Count == 0)
        {
            var filler = MasteredAttacks(parent)
                .OrderBy(attack => attack.CanInherit)
                .ThenBy(attack => attack.InternalName, StringComparer.Ordinal)
                .FirstOrDefault() ?? new RandomActiveSkill();
            loadout.Add(filler);
        }

        if (loadout.Count is < 1 or > 3)
            throw new InvalidOperationException("A parent loadout must contain one to three attacks.");
        return loadout;
    }

    private IEnumerable<ActiveSkill> MasteredAttacks(IPalReference reference) =>
        reference switch
        {
            SurgeryTablePalReference surgery => MasteredAttacks(surgery.Input),
            OwnedPalReference owned => owned.UnderlyingInstance.ActiveSkills ?? [],
            CompositeOwnedPalReference composite => composite.Male.UnderlyingInstance.ActiveSkills ?? [],
            BredPalReference { MaterializedAttackInheritance: not null } bred =>
                bred.MaterializedAttackInheritance.ChildMasteredAttacks,
            _ => reference.Pal.Level1ActiveSkills(settings.DB),
        };

    private AttackProfileEntry MaterializedEntryFor(
        BredPalReference reference,
        AttackProfileEntry entry
    ) => reference.Gender == PalGender.WILDCARD
        ? entry
        : entry.WithGuaranteedGender(
            settings.GameSettings,
            reference.Pal,
            reference.Parent1.TimeFactor,
            reference.Parent2.TimeFactor,
            settings.DB,
            reference.Gender,
            settings.UseGenderReversers
        );
}
