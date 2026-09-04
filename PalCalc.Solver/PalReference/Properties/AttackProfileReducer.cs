using System.Numerics;

namespace PalCalc.Solver.PalReference.Properties;

internal static class AttackProfileReducer
{
    private const int TargetMaskCount = AttackProfile.TargetMaskCount;

    public static AttackProfile Reduce(ReadOnlySpan<AttackProfileEntry> entries) =>
        Reduce(hasNoopAttack: false, entries);

    public static AttackProfile Reduce(
        bool hasNoopAttack,
        ReadOnlySpan<AttackProfileEntry> entries
    )
    {
        var accumulator = new Accumulator();
        accumulator.Reset(hasNoopAttack);
        foreach (ref readonly var entry in entries)
            accumulator.Add(entry);
        return accumulator.Build();
    }

    /// <summary>
    /// Keeps the minimum-cake champion for each of the at most 64 target masks.
    /// </summary>
    internal sealed class Accumulator
    {
        private readonly AttackProfileEntry[] champions = new AttackProfileEntry[TargetMaskCount];
        private bool hasNoopAttack;
        private ulong occupiedMasks;

        public bool HasNoopAttack => hasNoopAttack;
        public ulong OccupiedMasks => occupiedMasks;

        public void Reset(bool hasNoop)
        {
            hasNoopAttack = hasNoop;
            occupiedMasks = 0;
        }

        /// <summary>Returns whether this mask can improve its minimum cake cost.</summary>
        public bool CouldImprove(byte mask, int totalSpecialCakes) =>
            (occupiedMasks & (1UL << mask)) == 0 ||
            totalSpecialCakes < champions[mask].TotalSpecialCakes;

        public void Add(in AttackProfileEntry candidate)
        {
            var mask = candidate.LearnedTargetMask;
            var bit = 1UL << mask;
            if ((occupiedMasks & bit) != 0 &&
                champions[mask].TotalSpecialCakes <= candidate.TotalSpecialCakes)
                return;

            champions[mask] = candidate;
            occupiedMasks |= bit;
        }

        public AttackProfileEntry EntryForMask(int mask) => champions[mask];

        public int CalculateHashCode()
        {
            var result = HasNoopAttack ? 0b11 : 0b01;
            var masks = occupiedMasks;
            while (masks != 0)
            {
                var mask = BitOperations.TrailingZeroCount(masks);
                masks &= masks - 1;
                result = HashCode.Combine(result, champions[mask]);
            }
            return result;
        }

        public AttackProfile Build()
        {
            if (occupiedMasks == 0)
                return new AttackProfile(
                    hasNoopAttack,
                    Array.Empty<AttackProfileEntry>()
                );

            var retained = new AttackProfileEntry[BitOperations.PopCount(occupiedMasks)];
            var destination = 0;
            var masks = occupiedMasks;
            while (masks != 0)
            {
                var mask = BitOperations.TrailingZeroCount(masks);
                masks &= masks - 1;
                retained[destination++] = champions[mask];
            }

            return new AttackProfile(hasNoopAttack, retained);
        }
    }
}
