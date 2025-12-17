using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    public readonly record struct FPal(ushort Store)
    {
        public FPal(Pal pal) : this((ushort)pal.InternalIndex)
        {
        }

        public Pal ModelObject(PalDB db)
        {
            foreach (var p in db.Pals)
                if (p.InternalIndex == Store)
                    return p;

            throw new Exception($"Could not find pal matching InternalIndex={Store}");
        }
    }
}
