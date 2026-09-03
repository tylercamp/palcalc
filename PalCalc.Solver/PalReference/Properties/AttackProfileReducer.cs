using System.Numerics;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.PalReference.Properties;

internal static class AttackProfileReducer
{
    /// <summary>
    /// Returns whether obtaining <paramref name="provider"/> also satisfies
    /// <paramref name="required"/> under the attack solver's cake-first objective.
    /// </summary>
    public static bool Covers(in AttackProfileEntry provider, in AttackProfileEntry required) =>
        (provider.LearnedTargetMask & required.LearnedTargetMask) == required.LearnedTargetMask &&
        AttackProfileEntryComparer.CompareCosts(provider, required) <= 0;

    public static AttackProfile Reduce(ReadOnlySpan<AttackProfileEntry> entries) =>
        Reduce(hasNoopAttack: false, entries);

    public static AttackProfile Reduce(
        bool hasNoopAttack,
        ReadOnlySpan<AttackProfileEntry> entries,
        AttackSolverDiagnostics diagnostics = null
    )
    {
        var accumulator = new Accumulator(diagnostics);
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
    internal sealed class Accumulator(AttackSolverDiagnostics diagnostics = null)
    {
        private readonly AttackProfileEntry[] champions = new AttackProfileEntry[64];
        private readonly AttackProfileEntry[] values = new AttackProfileEntry[64];
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

        public void Add(in AttackProfileEntry candidate) => Add(candidate, candidate);

        public void Add(in AttackProfileEntry value, in AttackProfileEntry comparisonValue)
        {
            inputCount++;
            var mask = value.LearnedTargetMask;
            var bit = 1UL << mask;
            if ((occupiedMasks & bit) != 0 &&
                AttackProfileEntryComparer.CompareCosts(champions[mask], comparisonValue) <= 0)
                return;

            champions[mask] = comparisonValue;
            values[mask] = value;
            occupiedMasks |= bit;
        }

        public AttackProfile Build()
        {
            var retainedMasks = occupiedMasks;
            var candidates = occupiedMasks;
            while (candidates != 0)
            {
                var requiredMask = BitOperations.TrailingZeroCount(candidates);
                candidates &= candidates - 1;

                var providers = occupiedMasks & ~(1UL << requiredMask);
                while (providers != 0)
                {
                    var providerMask = BitOperations.TrailingZeroCount(providers);
                    providers &= providers - 1;
                    if ((providerMask & requiredMask) == requiredMask &&
                        AttackProfileEntryComparer.CompareCosts(
                            champions[providerMask], champions[requiredMask]
                        ) <= 0)
                    {
                        retainedMasks &= ~(1UL << requiredMask);
                        break;
                    }
                }
            }

            var retained = new AttackProfileEntry[BitOperations.PopCount(retainedMasks)];
            var destination = 0;
            while (retainedMasks != 0)
            {
                var mask = BitOperations.TrailingZeroCount(retainedMasks);
                retainedMasks &= retainedMasks - 1;
                retained[destination++] = values[mask];
            }

            var occupiedCount = BitOperations.PopCount(occupiedMasks);
            diagnostics?.RecordReduction(
                inputCount,
                retained.Length,
                occupiedCount
            );
            return new AttackProfile(hasNoopAttack, retained);
        }
    }
}
