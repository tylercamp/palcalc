using PalCalc.Model;
using PalCalc.Solver.Probabilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    /// <summary>
    /// An optimized store for up to 8 passive skills. Operations which combine PassiveSets
    /// will automatically deduplicate entries in the result (except for Random passives).
    /// </summary>
    public readonly record struct FPassiveSet(
        // 64-bit longs split into four ordered 16-bit items, sorted smallest (LSB, Lo) to largest (MSB, Hi)
        ulong StoreLo,
        ulong StoreHi
    )
    {
        public static readonly FPassiveSet Empty = new(0, 0);

        public static FPassiveSet RepeatRandom(int count)
        {
            ulong lo = 0;
            ulong hi = 0;

            for (int i = 0; i < count; i++)
                PushHigh(ref lo, ref hi, FPassive.Random.Store);

            return new FPassiveSet(lo, hi);
        }

        private const ulong LaneMask = 0xFFFFUL;

        // 128-bit logical shift right by 16:
        // [Hi:Lo] >>> 16
        private static void ShiftRight16(ref ulong lo, ref ulong hi)
        {
            lo = (lo >> 16) | (hi << 48);
            hi >>= 16;
        }

        private static void ShiftLeft16(ref ulong lo, ref ulong hi)
        {
            hi = (hi << 16) | (lo >> 48);
            lo <<= 16;
        }

        // Compact "keep" operation:
        // shift result right 16 bits and insert v in the top lane.
        private static void PushHigh(ref ulong resLo, ref ulong resHi, ushort v)
        {
            ShiftRight16(ref resLo, ref resHi);
            resHi |= (ulong)v << 48;
        }

        public static FPassiveSet FromModel(PalDB db, List<PassiveSkill> modelObjects)
        {
            if (modelObjects.Count > 8)
                throw new ArgumentException($"PassiveSet must not exceed 8 elements, but was provided {modelObjects.Count} elements");

            if (modelObjects.SkipNull().Distinct().Count() != modelObjects.SkipNull().Count())
                throw new ArgumentException($"PassiveSet requires deduplicated elements, but at least one was duplicated");

            foreach (var obj in modelObjects)
                if (obj != null && !db.PassiveSkills.Contains(obj) && obj is not RandomPassiveSkill)
                    throw new ArgumentException($"Passive '{obj}' was provided but not present in PalDB");

            var sorted = modelObjects.Select(m => new FPassive(db, m)).OrderBy(p => p.Store).ToList();

            ulong lo = 0, hi = 0;
            for (int i = 0; i < sorted.Count; i++)
                PushHigh(ref lo, ref hi, sorted[i].Store);

            return new FPassiveSet(lo, hi);
        }

        public static FPassiveSet Single(FPassive item)
        {
            ulong lo = 0, hi = 0;
            PushHigh(ref lo, ref hi, item.Store);

            return new FPassiveSet(lo, hi);
        }

        public int Count
        {
            get
            {
                int res = 0;
                ulong tmpLo = StoreLo, tmpHi = StoreHi;

                while (tmpHi != 0)
                {
                    ++res;
                    ShiftLeft16(ref tmpLo, ref tmpHi);
                }

#if DEBUG && DEBUG_CHECKS
                if (res != ModelObjects.Count()) Debugger.Break();
#endif

                return res;
            }
        }

        public int CountRandom
        {
            get
            {
                int res = 0;
                ulong tmpLo = StoreLo, tmpHi = StoreHi;

                // random is 0xFFFF and, if present, will always be in the left-most 16 bits of `Hi`
                while (tmpHi != 0)
                {
                    ushort highest = (ushort)(tmpHi >> 48);
                    if (highest == FPassive.Random.Store)
                        res++;
                    else
                        break;

                    ShiftLeft16(ref tmpLo, ref tmpHi);
                }

#if DEBUG && DEBUG_CHECKS
                if (res != ModelObjects.OfType<RandomPassiveSkill>().Count()) Debugger.Break();
#endif

                return res;
            }
        }

        public int CountNonRandom => Count - CountRandom;

        // (a non-empty set will have at least one value in the `hi` part)
        public bool IsEmpty => StoreHi == 0;

        public FPassive this[int i]
        {
            get
            {
                if (i < 0 || i >= 8) throw new IndexOutOfRangeException($"{i} is outside the range [0, 8)");

                ulong s;
                if (i < 4)
                {
                    s = StoreHi;
                }
                else
                {
                    s = StoreLo;
                    i -= 4;
                }

                return new FPassive((ushort)((s >> (i * 16)) & 0xFFFF));
            }
        }

        public IEnumerable<PassiveSkill> ModelObjects
        {
            get
            {
                for (int i = 0; i < 8; i++)
                {
                    // important: defer to `this[i]` so it's covered by unit tests
                    var p = this[i];
                    if (!p.IsEmpty) yield return p.ModelObject;
                }
            }
        }

        public bool Contains(FPassive passive) =>
            !passive.IsEmpty && !passive.IsRandom && !IsEmpty && (
            this[0] == passive ||
            this[1] == passive ||
            this[2] == passive ||
            this[3] == passive ||
            this[4] == passive ||
            this[5] == passive ||
            this[6] == passive ||
            this[7] == passive
        );

        public FPassiveSet Except(FPassiveSet others)
        {
            if (IsEmpty || others.IsEmpty) return this;

            ulong selfLo = StoreLo;
            ulong selfHi = StoreHi;
            ulong otherLo = others.StoreLo;
            ulong otherHi = others.StoreHi;
            ulong resLo = 0;
            ulong resHi = 0;

            while ((selfLo | selfHi) != 0)
            {
                // `sv` and `ov` contain the smallest of the remaining values
                // in each set
                ushort sv = (ushort)(selfLo & LaneMask);
                ushort ov = (ushort)(otherLo & LaneMask);

                if (sv == FPassive.Random.Store)
                {
                    // this is a 'Random' passive, which can't be matched against
                    // any other passives (even other Random passives), so this always
                    // gets kept
                    PushHigh(ref resLo, ref resHi, sv);
                    ShiftRight16(ref selfLo, ref selfHi);
                }
                else if (sv == ov)
                {
                    // value in self also appears in others: skip both
                    ShiftRight16(ref selfLo, ref selfHi);
                    ShiftRight16(ref otherLo, ref otherHi);
                }
                else if (sv > ov && (otherLo | otherHi) != 0)
                {
                    // all values in self are >= sv > ov, so advance others
                    ShiftRight16(ref otherLo, ref otherHi);
                }
                else
                {
                    // sv < ov, or others is empty:
                    // sv does not exist in others -> keep
                    PushHigh(ref resLo, ref resHi, sv);
                    ShiftRight16(ref selfLo, ref selfHi);
                }
            }

            return new FPassiveSet(resLo, resHi);
        }

        /// <param name="other">Must NOT be empty!</param>
        /// <returns></returns>
        public FPassiveSet Except(FPassive other)
        {
            if (IsEmpty || other.IsEmpty) return this; // empty
            if (other.IsRandom) return this; // 'Random' could be anything, "excluding" it is meaningless

            ulong selfLo = StoreLo, selfHi = StoreHi;
            ulong resLo = 0, resHi = 0;

            for (int i = 0; i < 8; i++)
            {
                if ((selfLo & 0xFFFF) != other.Store)
                {
                    PushHigh(ref resLo, ref resHi, (ushort)(selfLo & 0xFFFF));
                }

                ShiftRight16(ref selfLo, ref selfHi);
            }

            return new FPassiveSet(resLo, resHi);
        }

        public FPassiveSet Intersect(FPassiveSet others)
        {
            // ty chatgpt
            if (IsEmpty || others.IsEmpty)
                return new FPassiveSet(0, 0);

            ulong selfLo = StoreLo;
            ulong selfHi = StoreHi;
            ulong otherLo = others.StoreLo;
            ulong otherHi = others.StoreHi;
            ulong resLo = 0;
            ulong resHi = 0;

            while ((selfLo | selfHi) != 0 && (otherLo | otherHi) != 0)
            {
                ushort sv = (ushort)(selfLo & LaneMask);
                ushort ov = (ushort)(otherLo & LaneMask);

                // Random never intersects with anything, even another Random
                if (sv == FPassive.Random.Store)
                {
                    ShiftRight16(ref selfLo, ref selfHi);
                }
                else if (ov == FPassive.Random.Store)
                {
                    ShiftRight16(ref otherLo, ref otherHi);
                }
                else if (sv == ov)
                {
                    // common element -> keep it
                    PushHigh(ref resLo, ref resHi, sv);
                    ShiftRight16(ref selfLo, ref selfHi);
                    ShiftRight16(ref otherLo, ref otherHi);
                }
                else if (sv > ov)
                {
                    // advance other to catch up
                    ShiftRight16(ref otherLo, ref otherHi);
                }
                else // sv < ov
                {
                    // advance self to catch up
                    ShiftRight16(ref selfLo, ref selfHi);
                }
            }

            return new FPassiveSet(resLo, resHi);
        }

        // (note: preserves `Set` semantics, i.e. auto-deduplicates (not including Random))
        public FPassiveSet Concat(FPassiveSet other)
        {
            // ty chatgpt
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;

#if DEBUG && DEBUG_CHECKS
            if (other.Count + this.Count > 8) Debugger.Break();
#endif

            ulong selfLo = StoreLo;
            ulong selfHi = StoreHi;
            ulong otherLo = other.StoreLo;
            ulong otherHi = other.StoreHi;
            ulong resLo = 0;
            ulong resHi = 0;

            while ((selfLo | selfHi) != 0 && (otherLo | otherHi) != 0)
            {
                ushort sv = (ushort)(selfLo & LaneMask);
                ushort ov = (ushort)(otherLo & LaneMask);

                if (sv == ov)
                {
                    // Non-special duplicate: keep one
                    if (sv != 0 && sv != FPassive.Random.Store)
                    {
                        PushHigh(ref resLo, ref resHi, sv);
                    }
                    else
                    {
                        // Random or Empty: keep both
                        PushHigh(ref resLo, ref resHi, sv);
                        PushHigh(ref resLo, ref resHi, ov);
                    }

                    ShiftRight16(ref selfLo, ref selfHi);
                    ShiftRight16(ref otherLo, ref otherHi);
                }
                else if (sv < ov)
                {
                    PushHigh(ref resLo, ref resHi, sv);
                    ShiftRight16(ref selfLo, ref selfHi);
                }
                else // sv > ov
                {
                    PushHigh(ref resLo, ref resHi, ov);
                    ShiftRight16(ref otherLo, ref otherHi);
                }
            }

            // Append remaining from self
            while ((selfLo | selfHi) != 0)
            {
                ushort sv = (ushort)(selfLo & LaneMask);
                PushHigh(ref resLo, ref resHi, sv);
                ShiftRight16(ref selfLo, ref selfHi);
            }

            // Append remaining from other
            while ((otherLo | otherHi) != 0)
            {
                ushort ov = (ushort)(otherLo & LaneMask);
                PushHigh(ref resLo, ref resHi, ov);
                ShiftRight16(ref otherLo, ref otherHi);
            }

            return new FPassiveSet(resLo, resHi);
        }

        public FPassiveSet Concat(FPassive other)
        {
#if DEBUG && DEBUG_CHECKS
            if (this.Count == 8) Debugger.Break();
#endif
            return Concat(Single(other));
        }

        /// Extract non-empty (Store != 0) values into buffer in sorted order.
        private int ExtractNonEmpty(Span<ushort> buffer)
        {
            ulong lo = StoreLo;
            ulong hi = StoreHi;
            int count = 0;

            while ((lo | hi) != 0 && count < buffer.Length)
            {
                ushort v = (ushort)(lo & LaneMask);
                if (v != 0)
                    buffer[count++] = v;

                ShiftRight16(ref lo, ref hi);
            }

            return count;
        }

        public CombinationIterator GetCombinationIterator(int k) => new CombinationIterator(this, k);

        public struct CombinationIterator
        {
            private readonly FPassiveSet _universe;
            private readonly int _n;
            private readonly int _k;
            private bool _started;

            // index state: current combination as indices into universe
            private int _i0, _i1, _i2, _i3;

            public FPassiveSet Current { get; private set; }

            internal CombinationIterator(FPassiveSet universe, int k)
            {
                if (k < 1 || k > 4)
                    throw new ArgumentOutOfRangeException(nameof(k), "k must be between 1 and 4.");

                _universe = universe;
                _k = k;
                _started = false;
                _i0 = _i1 = _i2 = _i3 = 0;
                Current = default;

                Span<ushort> elems = stackalloc ushort[8];
                _n = universe.ExtractNonEmpty(elems);
                if (_n < k)
                    throw new InvalidOperationException($"Universe has only {_n} elements, need at least {k}.");
            }

            public bool MoveNext()
            {
                if (!_started)
                {
                    // first combination: [0,1,2,...,k-1]
                    _i0 = 0;
                    if (_k > 1) _i1 = 1;
                    if (_k > 2) _i2 = 2;
                    if (_k > 3) _i3 = 3;

                    Current = BuildCurrent();
                    _started = true;
                    return true;
                }

                if (!NextCombinationIndices(_n, _k, ref _i0, ref _i1, ref _i2, ref _i3))
                    return false;

                Current = BuildCurrent();
                return true;
            }

            private FPassiveSet BuildCurrent()
            {
                Span<ushort> elems = stackalloc ushort[8];
                int n = _universe.ExtractNonEmpty(elems); // n == _n, but cheap anyway

                ulong lo = 0, hi = 0;

                switch (_k)
                {
                    case 4:
                        PushHigh(ref lo, ref hi, elems[_i0]);
                        PushHigh(ref lo, ref hi, elems[_i1]);
                        PushHigh(ref lo, ref hi, elems[_i2]);
                        PushHigh(ref lo, ref hi, elems[_i3]);
                        break;
                    case 3:
                        PushHigh(ref lo, ref hi, elems[_i0]);
                        PushHigh(ref lo, ref hi, elems[_i1]);
                        PushHigh(ref lo, ref hi, elems[_i2]);
                        break;
                    case 2:
                        PushHigh(ref lo, ref hi, elems[_i0]);
                        PushHigh(ref lo, ref hi, elems[_i1]);
                        break;
                    case 1:
                        PushHigh(ref lo, ref hi, elems[_i0]);
                        break;
                }

                return new FPassiveSet(lo, hi);
            }

            // Lexicographic next k-combination; indices stored in the ref fields.
            private static bool NextCombinationIndices(
                int n, int k,
                ref int i0, ref int i1, ref int i2, ref int i3)
            {
                // We’ll operate through a tiny local array for clarity, then write back.
                Span<int> idx = stackalloc int[4];
                idx[0] = i0;
                idx[1] = i1;
                idx[2] = i2;
                idx[3] = i3;

                for (int i = k - 1; i >= 0; i--)
                {
                    if (idx[i] != i + n - k)
                    {
                        idx[i]++;
                        for (int j = i + 1; j < k; j++)
                            idx[j] = idx[j - 1] + 1;

                        i0 = idx[0];
                        if (k > 1) i1 = idx[1];
                        if (k > 2) i2 = idx[2];
                        if (k > 3) i3 = idx[3];
                        return true;
                    }
                }

                return false; // already last combination
            }
        }
    }
}
