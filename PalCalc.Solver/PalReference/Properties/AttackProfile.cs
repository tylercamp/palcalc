using PalCalc.Model;
using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.PalReference.Properties;

public readonly record struct AttackProfileEntry(
    byte MasteredTargetMask,
    int TotalSpecialCakes,
    TimeSpan BreedingEffort,
    int SelfBreedings,
    bool SelfUsesSpecialCake
)
{
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
}

public readonly struct AttackProfile : IEquatable<AttackProfile>
{
    private readonly AttackProfileEntry[] entries;

    private AttackProfile(bool inactive)
    {
        entries = null;
        HasNeutralAttack = false;
    }

    private AttackProfile(AttackProfileEntry[] entries, bool hasNeutralAttack)
    {
        this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
        HasNeutralAttack = hasNeutralAttack;
    }

    public AttackProfile(params AttackProfileEntry[] entries)
        : this(entries, hasNeutralAttack: false)
    {
    }

    public AttackProfile(bool hasNeutralAttack, params AttackProfileEntry[] entries)
        : this(entries, hasNeutralAttack)
    {
    }

    public static AttackProfile Inactive { get; } = new(true);

    public bool HasNeutralAttack { get; }

    public IReadOnlyList<AttackProfileEntry> Entries => entries ?? Array.Empty<AttackProfileEntry>();

    public bool Contains(byte requiredMask) => entries?.Any(entry =>
        (entry.MasteredTargetMask & requiredMask) == requiredMask
    ) == true;

    public bool Equals(AttackProfile other)
    {
        if (HasNeutralAttack != other.HasNeutralAttack)
            return false;
        if (ReferenceEquals(entries, other.entries))
            return true;
        if (entries is null || other.entries is null)
            return false;
        return entries.SequenceEqual(other.entries);
    }

    public override bool Equals(object obj) => obj is AttackProfile other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(HasNeutralAttack);
        hash.Add(entries is null);
        foreach (var entry in Entries)
            hash.Add(entry);
        return hash.ToHashCode();
    }

    internal AttackProfile WithGuaranteedGender(
        GameSettings gameSettings,
        Pal pal,
        float parent1TimeFactor,
        float parent2TimeFactor,
        PalDB db,
        PalGender gender,
        bool useReverser
    ) => entries is null
        ? Inactive
        : new AttackProfile(HasNeutralAttack, entries.Select(entry => entry.WithGuaranteedGender(
            gameSettings, pal, parent1TimeFactor, parent2TimeFactor, db, gender, useReverser
        )).ToArray());
}
