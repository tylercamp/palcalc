using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public class PalSpecifier
    {
        public Pal Pal { get; set; }
        public List<Trait> Traits { get; set; }

        public override string ToString() => $"{Pal.Name} with {Traits.TraitsListToString()}";
    }
}
