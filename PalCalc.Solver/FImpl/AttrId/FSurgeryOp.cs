using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    public readonly record struct FSurgeryOp(byte Store)
    {
        private const byte MASK_TYPE    = 0b00_000_111;
        private const byte MASK_ADD_IDX = 0b00_111_000; // options have at most 8 passives, only need 3 bits
        private const byte MASK_DEL_IDX = 0b11_000_000; // pal has at most 4 passives, only need 2 bits

        private const byte TYPE_NONE = 0b000;
        private const byte TYPE_CHANGE_GENDER = 0b001;
        private const byte TYPE_ADD_PASSIVE = 0b010;
        private const byte TYPE_REPLACE_PASSIVE = 0b100;

        private static byte ValidatedIndex(int idx)
        {
#if DEBUG && DEBUG_CHECKS
            if (idx < 0) throw new IndexOutOfRangeException();
#endif
            return (byte)idx;
        }

        public static readonly FSurgeryOp None = new(TYPE_NONE);


        /* Values for gender-change op */

        // (reuse MASK_ADD_IDX bits to store target gender)
        public static FSurgeryOp ChangeGender(FGender newGender) =>
            new((byte)(TYPE_CHANGE_GENDER | (newGender.Store << 3)));

        public bool IsGenderChange => (Store & MASK_TYPE) == TYPE_CHANGE_GENDER;

        public FGender GenderChange_Value => new((byte)((Store & MASK_ADD_IDX) >> 3));

        /* Values for add-passive op */

        public static FSurgeryOp AddPassive(FPassiveSet refSet, FPassive passive) =>
            new((byte)(
                TYPE_ADD_PASSIVE |
                (ValidatedIndex(refSet.IndexOf(passive)) << 3)
            ));

        public bool IsAddPassive => (Store & MASK_TYPE) == TYPE_ADD_PASSIVE;

        public FPassive AddPassive_NewPassive(FPassiveSet refSet) => refSet[(Store & MASK_ADD_IDX) >> 3];

        /* Values for replace-passive op */

        public static FSurgeryOp ReplacePassive(FPassiveSet originalPassives, FPassive removedPassive, FPassiveSet refSet, FPassive addedPassive)
        {
            var removedIdx = ValidatedIndex(originalPassives.IndexOf(removedPassive));
            var addedIdx = ValidatedIndex(refSet.IndexOf(addedPassive));

            return new((byte)(
                TYPE_REPLACE_PASSIVE |
                ((removedIdx << 6) & MASK_DEL_IDX) |
                ((addedIdx << 3) & MASK_ADD_IDX)
            ));
        }

        public bool IsReplacePassive => (Store & MASK_TYPE) == TYPE_REPLACE_PASSIVE;

        public int ReplacePassive_RemovedIndex => (Store & MASK_DEL_IDX) >> 6;
        public int ReplacePassive_AddedIndex => (Store & MASK_ADD_IDX) >> 3;
    }
}
