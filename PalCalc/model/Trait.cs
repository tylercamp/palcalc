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

        public static Trait Random => new Trait() { Name = "(Random)", InternalName = "__VIRT_RAND__" };
    }
}
