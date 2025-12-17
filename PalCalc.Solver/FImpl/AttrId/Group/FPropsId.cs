using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId.Group
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct FPropsId(byte NumSteps, byte NumWildPals)
    {
    }
}
