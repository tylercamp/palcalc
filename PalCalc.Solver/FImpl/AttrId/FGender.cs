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
        public FGender(PalGender value) : this((byte)value) { }

        public PalGender Value => (PalGender)Store;
    }
}
