using System.Numerics;

namespace PalCalc.Solver.PalReference.Properties;

/// <summary>
/// <para>
///     Tracks a distinct list of possible attack outcomes for a given Pal. As children
///     are bred, the profiles of the parents are accumulated to track available paths.
/// </para>
/// <para>
///     These attack outcomes are exclusive; i.e., though the profile's masks
///     may cover all desired attacks, it might not be possible for the child Pal
///     to actually obtain all the attacks at once. This is affected by Palworld's
///     limit of 3 attacks per parent, and the specific distribution of attacks among
///     this Pal's parents.
/// </para>
/// <para>
///     During search, the profile only tracks which attack outcomes are possible.
///     Each bred candidate has one breeding method and its own passive probability;
///     every mask respects that method. Effort and cake estimates are stored on
///     the breeding path instead of in the profile. The search keeps cheaper,
///     slower paths alongside faster paths that use more cakes.
/// </para>
/// <para>
///     `AttackResultMaterializer` chooses the exact loadouts and records them in
///     `MaterializedAttackInheritance`, recalculating attack probability, effort,
///     and gender-adjusted cake use. It may reconstruct alternatives with higher
///     cake costs and lower effort, or reject a surviving path once its actual
///     costs are known.
/// </para>
/// </summary>
public readonly record struct AttackProfile
{
    // Required attacks occupy one bit each. The six-attack request limit therefore
    // bounds every profile to the 64 possible values of a byte-sized target mask.
    internal const int TargetMaskCount = 1 << PalSpecifier.MaxRequiredAttacks;

    internal AttackProfile(ulong entryTargetMasks, bool hasNoopAttack)
    {
        EntryTargetMasks = entryTargetMasks;
        HasNoopAttack = hasNoopAttack;
    }

    public AttackProfile(params byte[] masks) : this(false, masks) { }

    public AttackProfile(bool hasNoopAttack, params byte[] masks)
    {
        HasNoopAttack = hasNoopAttack;
        foreach (var mask in masks)
        {
            if (mask >= TargetMaskCount)
                throw new ArgumentOutOfRangeException(nameof(masks));
            EntryTargetMasks |= 1UL << mask;
        }
    }

    /// <summary>
    /// The attack profile used when attack solving is disabled for the current run.
    /// </summary>
    public static AttackProfile Inactive => default;

    /// <summary>
    /// Whether the pal has a pal-specific, "non-inheritable" attack.
    ///
    /// When Palworld has to roll for attack inheritance and it builds the list of selectable
    /// attacks, a non-inheritable attack will be ignored, which is a "no-op" on the final
    /// list of available attacks.
    ///
    /// This is important - if there are two parent pals, and each has 1 attack equipped, the
    /// parent with a non-inheritable attack is basically skipped. This means the attack from
    /// the other parent gets a 100% chance to be inherited.
    ///
    /// (This is only relevant for single-attack inheritance, i.e., no special cakes.)
    /// </summary>
    public bool HasNoopAttack { get; }

    // Bit N records whether this profile contains the exact target mask N.
    // The bits within each target mask are decided by AttackTargetContext for the current run.
    //
    // Setting the first bit (bit 0) records a valid outcome with none of the requested attacks.
    //
    // But if every bit is zero, there are no feasible outcomes, including that zero-attack outcome.
    // We also use this empty representation for Inactive when attack solving is disabled.
    internal ulong EntryTargetMasks { get; }

    /// <summary>
    /// Whether this profile has any valid attack outcomes.
    /// 
    /// Note: "No attacks" is a valid attack outcome.
    /// </summary>
    public bool HasOutcomes => EntryTargetMasks != 0;

    public IEnumerable<byte> Masks => EnumerateMasks(EntryTargetMasks);

    private static IEnumerable<byte> EnumerateMasks(ulong masks)
    {
        while (masks != 0)
        {
            yield return (byte)BitOperations.TrailingZeroCount(masks);
            masks &= masks - 1;
        }
    }

    /// <summary>
    /// Returns whether any of the contained outcomes can fully cover the requirements of
    /// the given mask. (This is <em>not</em> the same as whether the exact mask is
    /// contained - this allows for supersets.)
    /// </summary>
    public bool Contains(byte requiredMask)
    {
        var masks = EntryTargetMasks;
        while (masks != 0)
        {
            var mask = BitOperations.TrailingZeroCount(masks);
            if ((mask & requiredMask) == requiredMask)
                return true;
            masks &= masks - 1;
        }
        return false;
    }
}
