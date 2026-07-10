using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public record class GenderedPal(Pal Pal, PalGender Gender)
    {
        public bool Matches(PalInstance inst) => inst.Pal == Pal && inst.Gender == Gender;

        public override string ToString() => $"{Gender} {Pal}";
    }
}
