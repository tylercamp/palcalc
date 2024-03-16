using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.model
{
    internal class Trait
    {
        public string Name { get; set; }
        public string InternalName { get; set; }

        public override string ToString() => Name;
    }
}
