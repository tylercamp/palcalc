using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    public readonly record struct FTime(int Store)
    {
        public FTime(TimeSpan time) : this((int)time.TotalSeconds)
        {
        }

        public TimeSpan Value => TimeSpan.FromSeconds(Store);
    }
}
