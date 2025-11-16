using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId.Group
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public record struct FCostId(FTime Time, FGold Gold, FReversers Reversers)
    {
    }
}
