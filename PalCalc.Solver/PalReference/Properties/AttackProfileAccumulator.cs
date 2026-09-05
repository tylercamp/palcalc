using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference.Properties
{
    /// <summary>
    /// Accepts an arbitrary number of AttackProfiles, tracking all possible attack combinations,
    /// and only keeps the most cake-efficient option for each visited attack combination.
    /// 
    /// Must be reset before each use.
    /// </summary>
    internal sealed class AttackProfileAccumulator
    {
        // (There are, at most, 6 attacks that can be considered for inheritance, due to
        // the limit on Special Cakes. Every attack is either present or absent, and we
        // don't care for attack order, so there are up-to 2^6 (64) possible attack
        // combinations.)

        private const int TargetMaskCount = AttackProfile.TargetMaskCount;

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

        /// <summary>
        /// Registers the candidate and preserves it if it's the least expensive option.
        /// Candidates can be given from different AttackProfiles, but all source AttackProfiles
        /// must have the same value for `HasNoopAttack`.
        /// </summary>
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
