using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public readonly record struct FIVSet(FIV Attack, FIV Defense, FIV HP)
    {
        public static readonly FIVSet AllRandom = new(FIV.Random, FIV.Random, FIV.Random);

        public static FIVSet Merge(FIVSet a, FIVSet b) =>
            new(
                Attack: FIV.Merge(a.Attack, b.Attack),
                Defense: FIV.Merge(a.Defense, b.Defense),
                HP: FIV.Merge(a.HP, b.HP)
            );

        public IV_Set ModelObject => new() { Attack = Attack.ModelObject, Defense = Defense.ModelObject, HP = HP.ModelObject };
    }
}
