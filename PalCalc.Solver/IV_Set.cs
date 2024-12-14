using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public class IV_Set
    {
        public IV_Set() { }
        public IV_Set(IV_Set other)
        {
            HP = other.HP;
            Attack = other.Attack;
            Defense = other.Defense;
        }

        public IV_IValue HP { get; set; }
        public IV_IValue Attack { get; set; }
        public IV_IValue Defense { get; set; }

        public override int GetHashCode() =>
            HashCode.Combine(
                HP,
                Attack,
                Defense
            );
    }
}
