using PalCalc.Model;
using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.PalReference.Properties;

/// <summary>
///     Describes a set of desired attacks that can be obtained by a Pal, and the
///     various costs involved in obtaining this result.
/// </summary>
/// 
/// <param name="LearnedTargetMask">
///     A bit-mask describing which desired attacks are covered by this entry. The structure
///     of this mask is decided by the `AttackTargetContext` for the current solver run.
/// </param>
/// 
/// <param name="TotalSpecialCakes">
///     The total number of Special Cakes needed throughout the complete breeding tree for this result.
/// </param>
/// 
/// <param name="BreedingEffort">
///     The total breeding effort to produce the complete breeding tree for this result.
/// </param>
/// 
/// <param name="SelfBreedings">
///     The number of breeding attempts needed at the final step to produce this Pal.
/// </param>
/// 
/// <param name="SelfUsesSpecialCake">
///     Whether the final breeding step requires the use of Special Cakes.
/// </param>
public readonly record struct AttackProfileEntry(
    byte LearnedTargetMask,
    int TotalSpecialCakes,
    TimeSpan BreedingEffort,
    int SelfBreedings,
    bool SelfUsesSpecialCake
)
{
    // TODO - adjust param order, add comments
    internal AttackProfileEntry WithGuaranteedGender(
        GameSettings gameSettings,
        Pal pal,
        float parent1TimeFactor,
        float parent2TimeFactor,
        PalDB db,
        PalGender gender,
        bool useReverser
    )
    {
        var adjustedBreedings = BredPalReferenceEffort.WithGuaranteedGender(
            SelfBreedings, pal, db, gender, useReverser
        );
        var oldSelfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            gameSettings, pal, parent1TimeFactor, parent2TimeFactor, SelfBreedings
        );
        var newSelfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            gameSettings, pal, parent1TimeFactor, parent2TimeFactor, adjustedBreedings
        );

        return this with
        {
            SelfBreedings = adjustedBreedings,
            BreedingEffort = BreedingEffort - oldSelfEffort + newSelfEffort,
            TotalSpecialCakes = TotalSpecialCakes + (SelfUsesSpecialCake ? adjustedBreedings - SelfBreedings : 0),
        };
    }

    public static AttackProfileEntry WildPalLevel1Attack(byte attackMask, TimeSpan captureEffort) =>
        new(
            LearnedTargetMask: attackMask,
            TotalSpecialCakes: 0,
            BreedingEffort: captureEffort,
            SelfBreedings: 0,
            SelfUsesSpecialCake: false
        );
}

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
///     Profiles are deliberately bounded. For each requested-attack mask the solver keeps
///     the outcome using the fewest Special Cakes, then the least breeding effort. An
///     equal-or-better superset can replace a subset outcome.
/// </para>
/// <para>
///     The final, exact choice of attacks is decided in the final solver steps, using
///     `AttackResultMaterializer` and `MaterializedAttackInheritance`.
/// </para>
/// </summary>
public readonly struct AttackProfile : IEquatable<AttackProfile>
{
    private readonly AttackProfileEntry[] entries;
    private readonly int hash;

    private AttackProfile(bool inactive)
    {
        entries = null;
        HasNoopAttack = false;
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
            hash = HashCode.Combine(hash, entry);
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

    internal AttackProfile WithGuaranteedGender(
        GameSettings gameSettings,
        Pal pal,
        float parent1TimeFactor,
        float parent2TimeFactor,
        PalDB db,
        PalGender gender,
        bool useReverser
    )
    {
        if (entries is null)
            return Inactive;

        var resultEntries = new AttackProfileEntry[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            resultEntries[i] = entries[i].WithGuaranteedGender(gameSettings, pal, parent1TimeFactor, parent2TimeFactor, db, gender, useReverser);

        return new AttackProfile(HasNoopAttack, resultEntries);
    }
}
