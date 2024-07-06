using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class GenderedPal
    {
        public Pal Pal { get; set; }
        public PalGender Gender { get; set; }

        public bool Matches(PalInstance inst) => inst.Pal == Pal && inst.Gender == Gender;

        public override bool Equals(object obj) => obj is GenderedPal && obj.GetHashCode() == GetHashCode();
        public override int GetHashCode() => HashCode.Combine(Pal, Gender);

        public override string ToString() => $"{Gender} {Pal}";
    }
}
