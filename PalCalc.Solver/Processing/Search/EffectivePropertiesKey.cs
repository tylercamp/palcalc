using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Processing.Search;

internal readonly struct PassiveSetKey : IEquatable<PassiveSetKey>
{
    private const int Capacity = 4;

    private readonly string passive0;
    private readonly string passive1;
    private readonly string passive2;
    private readonly string passive3;
    private readonly int hashCode;

    public PassiveSetKey(IReadOnlyList<PassiveSkill> passives)
    {
        ArgumentNullException.ThrowIfNull(passives);

        if (passives.Count > Capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passives),
                $"A passive key cannot contain more than {Capacity} passives."
            );
        }

        Count = (byte)passives.Count;
        passive0 = passives.Count > 0 ? passives[0].InternalName : null;
        passive1 = passives.Count > 1 ? passives[1].InternalName : null;
        passive2 = passives.Count > 2 ? passives[2].InternalName : null;
        passive3 = passives.Count > 3 ? passives[3].InternalName : null;

        // Sorting network for four values. Missing values are null and remain at
        // the end. This avoids allocating a temporary collection in the hot path.
        Sort(ref passive0, ref passive1);
        Sort(ref passive2, ref passive3);
        Sort(ref passive0, ref passive2);
        Sort(ref passive1, ref passive3);
        Sort(ref passive1, ref passive2);

        hashCode = Count == 0
            ? 0
            : HashCode.Combine(
                Count,
                passive0,
                passive1,
                passive2,
                passive3
            );
    }

    public byte Count { get; }

    public bool Equals(PassiveSetKey other) =>
        Count == other.Count &&
        StringComparer.Ordinal.Equals(passive0, other.passive0) &&
        StringComparer.Ordinal.Equals(passive1, other.passive1) &&
        StringComparer.Ordinal.Equals(passive2, other.passive2) &&
        StringComparer.Ordinal.Equals(passive3, other.passive3);

    public override bool Equals(object obj) =>
        obj is PassiveSetKey other && Equals(other);

    public override int GetHashCode() => hashCode;

    public static bool operator ==(PassiveSetKey left, PassiveSetKey right) =>
        left.Equals(right);

    public static bool operator !=(PassiveSetKey left, PassiveSetKey right) =>
        !left.Equals(right);

    private static void Sort(ref string left, ref string right)
    {
        if (
            right != null &&
            (left == null || string.CompareOrdinal(left, right) > 0)
        )
        {
            (left, right) = (right, left);
        }
    }
}

internal readonly struct RelevantIVKey : IEquatable<RelevantIVKey>
{
    private const byte HPRelevant = 1 << 0;
    private const byte AttackRelevant = 1 << 1;
    private const byte DefenseRelevant = 1 << 2;

    private readonly byte relevance;

    public RelevantIVKey(IV_Set ivs)
    {
        relevance = 0;
        if (ivs.HP.IsRelevant) relevance |= HPRelevant;
        if (ivs.Attack.IsRelevant) relevance |= AttackRelevant;
        if (ivs.Defense.IsRelevant) relevance |= DefenseRelevant;
    }

    public bool HP => (relevance & HPRelevant) != 0;
    public bool Attack => (relevance & AttackRelevant) != 0;
    public bool Defense => (relevance & DefenseRelevant) != 0;

    public bool Equals(RelevantIVKey other) => relevance == other.relevance;

    public override bool Equals(object obj) =>
        obj is RelevantIVKey other && Equals(other);

    public override int GetHashCode() => relevance;

    public static bool operator ==(RelevantIVKey left, RelevantIVKey right) =>
        left.Equals(right);

    public static bool operator !=(RelevantIVKey left, RelevantIVKey right) =>
        !left.Equals(right);
}

/// <summary>
/// Groups candidates that are interchangeable in future breeding steps.
/// Effort, cost, and breeding history are intentionally excluded because the
/// selection policy compares those details within each group.
/// </summary>
internal readonly struct EffectivePropertiesKey : IEquatable<EffectivePropertiesKey>
{
    private readonly int hashCode;

    public EffectivePropertiesKey(
        PalId palId,
        PalGender gender,
        PassiveSetKey passives,
        RelevantIVKey ivs
    )
    {
        ArgumentNullException.ThrowIfNull(palId);

        PalDexNo = palId.PalDexNo;
        IsPalVariant = palId.IsVariant;
        Gender = gender;
        Passives = passives;
        IVs = ivs;
        hashCode = HashCode.Combine(
            PalDexNo,
            IsPalVariant,
            Gender,
            Passives,
            IVs
        );
    }

    public int PalDexNo { get; }
    public bool IsPalVariant { get; }
    public PalGender Gender { get; }
    public PassiveSetKey Passives { get; }
    public RelevantIVKey IVs { get; }

    public bool Equals(EffectivePropertiesKey other) =>
        PalDexNo == other.PalDexNo &&
        IsPalVariant == other.IsPalVariant &&
        Gender == other.Gender &&
        Passives == other.Passives &&
        IVs == other.IVs;

    public override bool Equals(object obj) =>
        obj is EffectivePropertiesKey other && Equals(other);

    public override int GetHashCode() => hashCode;

    public static bool operator ==(EffectivePropertiesKey left, EffectivePropertiesKey right) =>
        left.Equals(right);

    public static bool operator !=(EffectivePropertiesKey left, EffectivePropertiesKey right) =>
        !left.Equals(right);
}

internal interface IEffectivePropertiesKeyProvider
{
    EffectivePropertiesKey KeyOf(IPalReference reference);
}

internal sealed class DefaultEffectivePropertiesKeyProvider : IEffectivePropertiesKeyProvider
{
    public static DefaultEffectivePropertiesKeyProvider Instance { get; } = new();

    private DefaultEffectivePropertiesKeyProvider() { }

    public EffectivePropertiesKey KeyOf(IPalReference reference) =>
        new(
            palId: reference.Pal.Id,
            gender: reference.Gender,
            passives: new PassiveSetKey(reference.EffectivePassives),
            ivs: new RelevantIVKey(reference.IVs)
        );
}
