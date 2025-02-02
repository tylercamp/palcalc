using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class Human
    {
        public Human(string internalName)
        {
            InternalName = internalName;
        }

        public string InternalName { get; set; }

        public override string ToString() => $"{InternalName} (Human)";

        public override bool Equals(object obj) => (obj as Human)?.GetHashCode() == GetHashCode();

        public override int GetHashCode() => HashCode.Combine(typeof(Human).GetHashCode(), InternalName);
    }
}
