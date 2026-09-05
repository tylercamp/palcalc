using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System.Collections.Frozen;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// The requested-attack state guaranteed for a Pal species, based on
/// their Lv1 attacks and whether those attacks can be inherited.
/// </summary>
internal readonly record struct SpeciesAttackState(
    byte Level1TargetMask,
    bool HasNooplLevel1Attack
);

/// <summary>
/// Used by a single solver run to map required attacks to a bitfield.
/// </summary>
internal sealed class AttackTargetContext
{
    private readonly PalDB db;
    private readonly PalSpecifier target;
    private readonly ActiveSkill[] requiredAttacks;
    private readonly FrozenDictionary<Pal, SpeciesAttackState> speciesStates;

    public AttackTargetContext(PalSpecifier target, PalDB db)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(db);

        this.db = db;
        this.target = target;
        requiredAttacks = target.RequiredAttacks.Distinct().ToArray();
        if (requiredAttacks.Length > PalSpecifier.MaxRequiredAttacks)
            throw new ArgumentOutOfRangeException(nameof(target), $"At most {PalSpecifier.MaxRequiredAttacks} required attacks are supported.");

        IsActive = requiredAttacks.Length != 0;
        FullTargetMask = (byte)((1 << requiredAttacks.Length) - 1);
        InheritableTargetMask = MaskOf(requiredAttacks.Where(attack => attack.CanInherit));
        speciesStates = db.Pals.ToFrozenDictionary(pal => pal, CreateSpeciesState);
    }

    public bool IsActive { get; }
    public byte FullTargetMask { get; }
    public byte InheritableTargetMask { get; }

    public byte MaskOf(IEnumerable<ActiveSkill> attacks)
    {
        ArgumentNullException.ThrowIfNull(attacks);

        byte mask = 0;
        foreach (var attack in attacks)
        {
            var index = Array.IndexOf(requiredAttacks, attack);
            if (index >= 0)
                mask |= (byte)(1 << index);
        }

        return mask;
    }

    public ActiveSkill AttackForBit(byte singleBit)
    {
        if (singleBit == 0 || (singleBit & (singleBit - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(singleBit));

        var index = 0;
        while ((singleBit >>= 1) != 0)
            index++;

        return index < requiredAttacks.Length
            ? requiredAttacks[index]
            : throw new ArgumentOutOfRangeException(nameof(singleBit));
    }

    public bool Satisfies(IPalReference reference) =>
        target.IsSatisfiedByIgnoringAttacks(reference) &&
        (!IsActive || reference.AttackProfile.Contains(FullTargetMask));

    public SpeciesAttackState StateOf(Pal pal)
    {
        ArgumentNullException.ThrowIfNull(pal);
        return speciesStates.TryGetValue(pal, out var state)
            ? state
            : CreateSpeciesState(pal);
    }

    private SpeciesAttackState CreateSpeciesState(Pal pal)
    {
        var level1Attacks = pal.Level1ActiveSkills(db).ToArray();
        return new(
            Level1TargetMask: MaskOf(level1Attacks),
            HasNooplLevel1Attack: level1Attacks.Any(attack => !attack.CanInherit)
        );
    }
}