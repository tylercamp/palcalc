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
    /// Keeps one cake-first champion for each of the at most 64 target masks.
    /// This intentionally gives up faster cake-heavier alternatives: Special
    /// Cakes are the primary resource, and bounding every profile is required
    /// to keep multi-attack search tractable.
    /// </summary>
    internal sealed class Accumulator
    {
        private readonly AttackProfileEntry[] champions = new AttackProfileEntry[TargetMaskCount];
        private bool hasNoopAttack;
        private ulong occupiedMasks;
        private int inputCount;

        public int InputCount => inputCount;

        public void Reset(bool hasNoop)
        {
            hasNoopAttack = hasNoop;
            occupiedMasks = 0;
            inputCount = 0;
        }

        /// <summary>
        /// Cheap cake-only rejection used before the caller calculates the
        /// remaining entry costs. Equal cake counts may still improve effort.
        /// </summary>
        public bool CouldImprove(byte mask, int totalSpecialCakes) =>
            (occupiedMasks & (1UL << mask)) == 0 ||
            totalSpecialCakes <= champions[mask].TotalSpecialCakes;

        public void Add(in AttackProfileEntry candidate)
        {
            inputCount++;
            var mask = candidate.LearnedTargetMask;
            var bit = 1UL << mask;
            if ((occupiedMasks & bit) != 0 &&
                AttackProfileEntryComparer.CompareCosts(champions[mask], candidate) <= 0)
                return;

            champions[mask] = candidate;
            occupiedMasks |= bit;
        }

        public AttackProfile Build()
        {
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
