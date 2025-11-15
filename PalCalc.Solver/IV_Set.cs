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

        public override bool Equals(object obj) =>
            obj switch
            {
                null => ReferenceEquals(this, obj),
                IV_Set s => (
                    IV_IValue.AreEqual(s.HP, HP) &&
                    IV_IValue.AreEqual(s.Attack, Attack) &&
                    IV_IValue.AreEqual(s.Defense, Defense)
                ),
                _ => false
            };

        public override int GetHashCode() =>
            HashCode.Combine(
                HP,
                Attack,
                Defense
            );

        public override string ToString() => $"IVs(HP: {HP}, Attack: {Attack}, Defense: {Defense})";
    }
}
