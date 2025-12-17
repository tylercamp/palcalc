using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    public readonly record struct FGender(byte Store)
    {
        public static readonly FGender Male = new(PalGender.MALE);
        public static readonly FGender Female = new(PalGender.FEMALE);
        public static readonly FGender Wildcard = new(PalGender.WILDCARD);
        public static readonly FGender OppositeWildcard = new(PalGender.OPPOSITE_WILDCARD);

        public FGender(PalGender value) : this((byte)value) { }

        public PalGender Value => (PalGender)Store;

        public bool IsCompatible(FGender other) => Store == (byte)PalGender.WILDCARD || Store != other.Store;
    }
}
