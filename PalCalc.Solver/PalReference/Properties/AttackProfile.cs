namespace PalCalc.Solver.PalReference.Properties;

/// <summary>
///     During search, an attack profile records only exact requested-attack
///     availability and the minimum estimated Special Cake cost for each mask.
///     Attack probability, attack-specific effort, exact loadouts, and
///     gender-adjusted cake use are reconstructed after search. This deliberately
///     lossy boundary keeps the fixed attack search state small and portable:
///     a structurally slower path can be discarded even when its exact attack
///     odds would have been better, and a coarse finalist can fail an exact
///     constraint after reconstruction.
///     Search profile = availability + estimated cakes. Materialized inheritance
///     = exact probability + effort + cakes + loadouts.
/// </summary>
/// 
/// <param name="LearnedTargetMask">
///     A bit-mask describing which desired attacks are covered by this entry. The structure
///     of this mask is decided by the `AttackTargetContext` for the current solver run.
/// </param>
/// <param name="TotalSpecialCakes">
///     The minimum estimated number of Special Cakes needed throughout the
///     complete breeding tree for this exact outcome.
/// </param>
public readonly record struct AttackProfileEntry(
    byte LearnedTargetMask,
    int TotalSpecialCakes
);

/// <summary>
/// <para>
///     Manages a distinct list of possible attack outcomes for a given Pal. As children
///     are bred, the profiles of the parents are accumulated to track available paths.
/// </para>
/// <para>
///     These attack outcomes are exclusive; i.e., though the profile
///     entries may cover all desired attacks, it might not be possible for the child Pal
///     to actually obtain all the attacks at once. This is affected by Palworld's
///     limit of 3 attacks per parent, and the specific distribution of attacks among
///     this Pal's parents.
/// </para>
/// <para>
///     Profiles are deliberately bounded. For each requested-attack mask the
///     solver keeps the outcome using the fewest estimated Special Cakes.
/// </para>
/// <para>
///     The final, exact choice of attacks is decided in the final solver steps, using
///     `AttackResultMaterializer` and `MaterializedAttackInheritance`.
/// </para>
/// </summary>
public readonly struct AttackProfile : IEquatable<AttackProfile>
{
    // Required attacks occupy one bit each. The six-attack request limit therefore
    // bounds every profile to the 64 possible values of a byte-sized target mask.
    internal const int TargetMaskCount = 1 << PalSpecifier.MaxRequiredAttacks;

    private readonly AttackProfileEntry[] entries;
    private readonly int hash;

    private AttackProfile(bool inactive)
    {
        entries = null;
        HasNoopAttack = false;
        EntryTargetMasks = 0;
        hash = 0;
    }

    private AttackProfile(AttackProfileEntry[] entries, bool hasNoopAttack)
    {
        this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
        HasNoopAttack = hasNoopAttack;

        hash = 0b01;
        if (HasNoopAttack)
            hash |= 0b10;
        
        foreach (var entry in entries)
        {
            EntryTargetMasks |= 1UL << entry.LearnedTargetMask;
            hash = HashCode.Combine(hash, entry);
        }
    }

    public AttackProfile(params AttackProfileEntry[] entries)
        : this(entries, hasNoopAttack: false)
    {
    }

    public AttackProfile(bool hasNoopAttack, params AttackProfileEntry[] entries)
        : this(entries, hasNoopAttack)
    {
    }

    /// <summary>
    /// The attack profile used when attack solving is disabled for the current run.
    /// </summary>
    public static AttackProfile Inactive { get; } = new(true);

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

    // Bit N records whether this profile contains an entry for exact target mask N.
    internal ulong EntryTargetMasks { get; }

    /// <summary>
    /// WARNING: Provided for convenience, actual solver code should use `EntriesSpan`
    /// </summary>
    public IReadOnlyList<AttackProfileEntry> Entries => entries ?? [];

    public ReadOnlySpan<AttackProfileEntry> EntriesSpan => entries ?? [];

    public bool Contains(byte requiredMask)
    {
        if (entries == null)
            return false;

        // Unfolded `.Any()`
        foreach (var entry in entries)
        {
            if ((entry.LearnedTargetMask & requiredMask) == requiredMask)
                return true;
        }

        return false;
    }

    public bool Equals(AttackProfile other)
    {
        if (HasNoopAttack != other.HasNoopAttack)
            return false;
        if (ReferenceEquals(entries, other.entries))
            return true;
        if (entries is null || other.entries is null)
            return false;
        return entries.SequenceEqual(other.entries);
    }

    public override bool Equals(object obj) => obj is AttackProfile other && Equals(other);

    public override int GetHashCode() => hash;

}
