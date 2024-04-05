using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSaveReader.FArchive
{
    internal struct LinearColorLiteral
    {
        public float r, g, b, a;
    }

    struct QuaternionLiteral
    {
        public double x, y, z, w;
    }

    struct VectorLiteral
    {
        public double x, y, z;
    }
}
