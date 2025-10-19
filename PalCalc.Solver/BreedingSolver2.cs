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
                (isRelevant ? 0xF000 : 0)
                  | ((minValue & 0x7F) << 7)
                  | (maxValue & 0x7F)
            ))
        {
        }


        public bool IsRelevant => (Store & 0xF000) != 0;
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
        private const ushort RANDOM = unchecked(0x8000);
        public static Passive Random = new(RANDOM);

        public Passive(PassiveSkill ps) : this((ushort)(
            ps is RandomPassiveSkill ? RANDOM : (PalDB.SharedInstance.PassiveSkills.IndexOf(ps) + 1)
        ))
        {
        }

        public bool IsRandom => Store == RANDOM;
        public bool IsEmpty => Store == 0;

        public PassiveSkill ModelObject => IsEmpty ? null : (IsRandom ? new RandomPassiveSkill() : PalDB.SharedInstance.PassiveSkills[Store - 1]);
    }

    // 64-bit long split into four ordered 16-bit items
    public readonly record struct PassiveSet(long Store)
    {
        /// <param name="modelObjects">MUST be deduplicated!</param>
        /// <returns></returns>
        private static long Serialize(List<PassiveSkill> modelObjects)
        {
            var p1 = new Passive(modelObjects.Skip(0).FirstOrDefault()).Store;
            var p2 = new Passive(modelObjects.Skip(1).FirstOrDefault()).Store;
            var p3 = new Passive(modelObjects.Skip(2).FirstOrDefault()).Store;
            var p4 = new Passive(modelObjects.Skip(3).FirstOrDefault()).Store;

            // sort the ints
            if (p1 > p2) (p1, p2) = (p2, p1);
            if (p3 > p4) (p3, p4) = (p4, p3);
            if (p1 > p3) { (p1, p3) = (p3, p1); (p2, p4) = (p4, p2); }
            if (p2 > p3) (p2, p3) = (p3, p2);

            return (
                (((long)p1) << 0) |
                (((long)p2) << 16) |
                (((long)p3) << 32) |
                (((long)p4) << 48)
            );
        }

        public PassiveSet(List<PassiveSkill> passives)
            : this(Serialize(passives))
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
                    yield return this[i].ModelObject;
            }
        }

        public bool Contains(Passive passive) =>
            passive == this[0] ||
            passive == this[1] ||
            passive == this[2] ||
            passive == this[3];

        /// <param name="others">Must NOT be empty!</param>
        /// <returns></returns>
        public PassiveSet Except(PassiveSet others)
        {
            if (Store == 0 || others.Store == 0) return this;

            var remainingSelf = Store;
            var remainingOther = others.Store;

            long res = 0;
            for (int i = 0; i < 4; i++)
            {
                short sv = (short)(remainingSelf & 0xFFFF);
                short ov = (short)(remainingOther & 0xFFFF);

                if 

                res <<= 16;
                remainingSelf >>= 16;
                remainingOther >>= 16;
            }

            return res;
        }

        /// <param name="other">Must NOT be empty!</param>
        /// <returns></returns>
        public PassiveSet Except(Passive other)
        {
            if (Store == 0 || other.Store == 0) return this; // empty

            long remaining = Store;
            long result = 0;
            for (int i = 0; i < 4; i++)
            {
                result <<= 16;

                if ((remaining & other.Store) != other.Store)
                {
                    result |= remaining & 0xFFFF;
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
            int x = Marshal.SizeOf()
        }
    }
}
