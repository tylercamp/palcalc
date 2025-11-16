using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    // "empty" is stored as: 0x0000
    // "random" is stored as: 0x8000
    // actual passives are represented by array index in DB, +1
    public readonly record struct FPassive(ushort Store)
    {
        private const ushort RANDOM = 0xFFFF;
        public static readonly FPassive Random = new(RANDOM);
        public static readonly FPassive Empty = new(0);

        public FPassive(PalDB db, PassiveSkill ps) : this((ushort)(
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

        public readonly bool Equals(FPassive other) => !IsRandom && Store == other.Store;
        override public int GetHashCode() => Store;
    }
}
