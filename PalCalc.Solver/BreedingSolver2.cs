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
    public readonly record struct PassiveSet(ulong Store)
    {
        /// <param name="modelObjects">MUST be deduplicated!</param>
        private static ulong Serialize(PalDB db, List<PassiveSkill> modelObjects)
        {
            if (modelObjects.Count > 4)
                throw new ArgumentException($"PassiveSet must not exceed 4 elements, but was provided {modelObjects.Count} elements");

            if (modelObjects.Distinct().Count() != modelObjects.Count)
                throw new ArgumentException($"PassiveSet requires deduplicated elements, but at least one was duplicated");

            var p1 = new Passive(db, modelObjects.Skip(0).FirstOrDefault()).Store;
            var p2 = new Passive(db, modelObjects.Skip(1).FirstOrDefault()).Store;
            var p3 = new Passive(db, modelObjects.Skip(2).FirstOrDefault()).Store;
            var p4 = new Passive(db, modelObjects.Skip(3).FirstOrDefault()).Store;

            // sort the ints
            if (p1 > p2) (p1, p2) = (p2, p1);   // (0,1)
            if (p3 > p4) (p3, p4) = (p4, p3);   // (2,3)
            if (p1 > p3) (p1, p3) = (p3, p1);   // (0,2)
            if (p2 > p4) (p2, p4) = (p4, p2);   // (1,3)
            if (p2 > p3) (p2, p3) = (p3, p2);   // (1,2)

            return (
                (((ulong)p1) << 0) |
                (((ulong)p2) << 16) |
                (((ulong)p3) << 32) |
                (((ulong)p4) << 48)
            );
        }

        public PassiveSet(PalDB db, List<PassiveSkill> passives)
            : this(Serialize(db, passives))
        {
        }

        public Passive this[int i]
        {
            get => new((ushort)(
                (Store >> (i * 16)) & 0xFFFF
            ));
        }

        public IEnumerable<PassiveSkill> ModelObjects
        {
            get
            {
                for (int i = 0; i < 4; i++)
                {
                    var p = this[i];
                    if (!p.IsEmpty) yield return p.ModelObject;
                }
            }
        }

        public bool Contains(Passive passive) =>
            passive == this[0] ||
            passive == this[1] ||
            passive == this[2] ||
            passive == this[3];

        public PassiveSet Except(PassiveSet others)
        {
            if (Store == 0 || others.Store == 0) return this;

            var remainingSelf = Store;
            var remainingOther = others.Store;

            ulong res = 0;
            while (remainingSelf != 0)
            {
                // `sv` and `ov` contain the smallest of the remaining values
                // in each set
                ushort sv = (ushort)(remainingSelf & 0xFFFF);
                ushort ov = (ushort)(remainingOther & 0xFFFF);
                
                if (sv == Passive.Random.Store)
                {
                    // this is a 'Random' passive, which can't be matched against
                    // any other passives (even other Random passives), so this always
                    // gets kept

                    res >>= 16;
                    res |= (ulong)sv << 48;
                    remainingSelf >>= 16;
                }
                else if (sv == ov)
                {
                    // value in `self` which is also in `other`, skip
                    remainingSelf >>= 16;
                    remainingOther >>= 16;
                }
                else if (sv > ov && remainingOther != 0)
                {
                    // all values in `remainingSelf` are greater than `ov`
                    // shift `remainingOther` to skip the small value
                    //
                    // (but only do this if `remainingOther` is non-zero -- we're shifting
                    // `remainingOther` to get the next potential passive, but if the whole thing
                    // is empty, there's no point in shifting)
                    remainingOther >>= 16;
                }
                else // sv < ov -- value in `self` which isn't in `other`
                {
                    // all values in `remainingOther` are greater than `sv`, i.e.
                    // `sv` (self) does not exist in `ov` (excluded), and should be
                    // kept
                    res >>= 16;
                    res |= (ulong)sv << 48;

                    // value copied, remove from `self`
                    remainingSelf >>= 16;
                }
            }

            return new PassiveSet(res);
        }

        /// <param name="other">Must NOT be empty!</param>
        /// <returns></returns>
        public PassiveSet Except(Passive other)
        {
            if (Store == 0 || other.Store == 0) return this; // empty
            if (other.IsRandom) return this; // 'Random' could be anything, "excluding" it is meaningless

            ulong remaining = Store;
            ulong result = 0;
            for (int i = 0; i < 4; i++)
            {
                if ((remaining & 0xFFFF) != other.Store)
                {
                    result >>= 16;
                    result |= (remaining & 0xFFFF) << 48;
                }

                remaining >>= 16;
            }

            return new PassiveSet(result);
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
