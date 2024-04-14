using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public static class SolverExtensions
    {


        public static int NumWildPalParticipants(this IPalReference pref)
        {
            switch (pref)
            {
                case BredPalReference bpr: return NumWildPalParticipants(bpr.Parent1) + NumWildPalParticipants(bpr.Parent2);
                case OwnedPalReference opr: return 0;
                case WildPalReference wpr: return 1;
                default: throw new Exception($"Unhandled pal reference type {pref.GetType()}");
            }
        }

        public static int NumBredPalParticipants(this IPalReference pref)
        {
            switch (pref)
            {
                case BredPalReference bpr: return 1 + NumBredPalParticipants(bpr.Parent1) + NumBredPalParticipants(bpr.Parent2);
                default: return 0;
            }
        }
    }
}
