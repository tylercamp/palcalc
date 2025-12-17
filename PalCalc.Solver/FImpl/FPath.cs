using PalCalc.Solver.FImpl.AttrId;
using PalCalc.Solver.FImpl.AttrId.Group;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl
{
    public enum FPathType : byte
    {
        Breeding = 1,
        Surgery = 2,
        Owned = 3,
        Captured = 4,
    }

    [StructLayout(LayoutKind.Explicit)]
    public readonly struct FPath
    {
        private FPath(FPathType type)
        {
            this.Type = type;
        }

        public FPath(FTypeId breedingParent1, FTypeId breedingParent2)
        {
            Type = FPathType.Breeding;
            AsBreeding = new Breeding(breedingParent1, breedingParent2);
        }

        public static readonly FPath Owned = new(FPathType.Owned);
        public static readonly FPath Captured = new(FPathType.Captured);

        [FieldOffset(0)]
        public readonly FPathType Type;

        [FieldOffset(1)]
        public readonly Breeding AsBreeding;

        [FieldOffset(1)]
        public readonly Surgery AsSurgery;

        //// approx. 24 bytes
        public readonly record struct Breeding(
            FTypeId Parent1,
            FTypeId Parent2
        );

        // 
        public readonly record struct Surgery(
            // 12 bytes
            FTypeId Input,
            // 5 bytes (1 byte each)
            FSurgeryOp Op1,
            FSurgeryOp Op2,
            FSurgeryOp Op3,
            FSurgeryOp Op4,
            FSurgeryOp Op5
        );
    }
}
