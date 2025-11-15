using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using static PalCalc.Solver.AttrId;

namespace PalCalc.Solver
{
    

    interface AttrId
    {

    }

    public readonly record struct Gender(byte Store) : AttrId
    {
        public Gender(PalGender value) : this((byte)value) { }

        public PalGender Value => (PalGender)Store;
    }

    public readonly record struct Time(int Store) : AttrId
    {
        public Time(TimeSpan time) : this((int)time.TotalSeconds)
        {
        }

        public TimeSpan Value => TimeSpan.FromSeconds(Store);
    }

    // (note: use of `short` only works so long as IVs can't exceed 100)
    // first 2 bits are Type, next 7 are Range-Min, last 7 are Range-Max/Exact Value
    public readonly record struct IV(short Store)
    {
        public static readonly IV Random = new(0);

        public IV(bool isRelevant, int value) : this(isRelevant, value, value)
        {
        }

        public IV(bool isRelevant, int minValue, int maxValue)
            : this((short)(
                (isRelevant ? 0x8000 : 0)
                  | ((minValue & 0x7F) << 7)
                  | (maxValue & 0x7F)
            ))
        {
        }


        public bool IsRelevant => (Store & 0x8000) != 0;
        public int Max => Store & 0x7F;
        public int Min => (Store >> 7) & 0x7F;

        public IV_IValue ModelObject
        {
            get
            {
                if (this == Random) return IV_Random.Instance;

                return new IV_Range(IsRelevant, Min, Max);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public readonly record struct IVSet(IV Attack, IV Defense, IV HP) : AttrId
    {
        public IV_Set ModelObject => new() { Attack = Attack.ModelObject, Defense = Defense.ModelObject, HP = HP.ModelObject };
    }

    // "empty" is stored as: 0x0000
    // "random" is stored as: 0x8000
    // actual passives are represented by array index in DB, +1
    public readonly record struct Passive(ushort Store)
    {
        private const ushort RANDOM = unchecked(0xFFFF);
        public static readonly Passive Random = new(RANDOM);
        public static readonly Passive Empty = new(0);

        public Passive(PalDB db, PassiveSkill ps) : this((ushort)(
            ps is RandomPassiveSkill ? RANDOM : (db.PassiveSkills.IndexOf(ps) + 1)
        ))
        {
            if (ps is UnrecognizedPassiveSkill && !db.PassiveSkills.Contains(ps))
                // shouldn't happen if used correctly
                throw new Exception($"Unrecognized passive skill {ps.Name} does not exist in DB");
        }

        public bool IsRandom => Store == RANDOM;
        public bool IsEmpty => Store == 0;

        public PassiveSkill ModelObject => IsEmpty ? null : (IsRandom ? new RandomPassiveSkill() : PalDB.SharedInstance.PassiveSkills[Store - 1]);

        public readonly bool Equals(Passive other) => !IsRandom && Store == other.Store;
        override public int GetHashCode() => Store;
    }

    // 64-bit long split into four ordered 16-bit items, sorted smallest (LSB) to largest (MSB)
    public readonly record struct PassiveSet(ulong StoreLo, ulong StoreHi)
    {
        public static readonly PassiveSet Empty = new(0, 0);

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

        public static PassiveSet FromModel(PalDB db, List<PassiveSkill> modelObjects)
        {
            if (modelObjects.Count > 8)
                throw new ArgumentException($"PassiveSet must not exceed 8 elements, but was provided {modelObjects.Count} elements");

            if (modelObjects.Distinct().Count() != modelObjects.Count)
                throw new ArgumentException($"PassiveSet requires deduplicated elements, but at least one was duplicated");

            // TODO - efficient sort
            var sorted = modelObjects.Select(m => new Passive(db, m)).OrderBy(p => p.Store).ToList();

            ulong lo = 0, hi = 0;
            for (int i = 0; i < sorted.Count; i++)
                PushHigh(ref lo, ref hi, sorted[i].Store);

            return new PassiveSet(lo, hi);
        }

        public static PassiveSet Single(Passive item)
        {
            ulong lo = 0, hi = 0;
            PushHigh(ref lo, ref hi, item.Store);

            return new PassiveSet(lo, hi);
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

                return res;
            }
        }

        // (a non-empty set will have at least one value in the `hi` part)
        public bool IsEmpty => StoreHi == 0;

        public Passive this[int i]
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

                return new Passive((ushort)((s >> (i * 16)) & 0xFFFF));
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

        public bool Contains(Passive passive) =>
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

        public PassiveSet Except(PassiveSet others)
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
                // smallest remaining in self
                ushort sv = (ushort)(selfLo & LaneMask);
                ushort ov = (ushort)(otherLo & LaneMask);

                if (sv == Passive.Random.Store)
                {
                    // Random is never matched; always kept
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

            return new PassiveSet(resLo, resHi);
        }

        /// <param name="other">Must NOT be empty!</param>
        /// <returns></returns>
        public PassiveSet Except(Passive other)
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

            return new PassiveSet(resLo, resHi);
        }

        public PassiveSet Intersect(PassiveSet others)
        {
            if (IsEmpty || others.IsEmpty)
                return new PassiveSet(0, 0);

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
                if (sv == Passive.Random.Store)
                {
                    ShiftRight16(ref selfLo, ref selfHi);
                }
                else if (ov == Passive.Random.Store)
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

            return new PassiveSet(resLo, resHi);
        }

        // (note: preserves `Set` semantics, i.e. auto-deduplicates (not including Random))
        public PassiveSet Concat(PassiveSet other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;

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
                    if (sv != 0 && sv != Passive.Random.Store)
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

            return new PassiveSet(resLo, resHi);
        }

        public PassiveSet Concat(Passive other)
        {
            return Concat(Single(other));
        }
    }

    public readonly record struct AttrSet( // 24b
        PassiveSet Passives, // 8b
        IVSet IVs, // 8b
        Time Time, // 4b
        Gender Gender // 1b (4b)
    )
    { }


    public class BreedingSolver2(BreedingSolverSettings settings)
    {
        public List<IPalReference> SolveFor(PalSpecifier spec, SolverStateController controller)
        {
            throw new NotImplementedException();
        }
    }
}
