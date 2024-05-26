using PalCalc.Model;
using PalCalc.Solver.PalReference;
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
        public List<Trait> RequiredTraits { get; set; } = new List<Trait>();

        public List<Trait> OptionalTraits { get; set; } = new List<Trait>();

        public IEnumerable<Trait> DesiredTraits => RequiredTraits.Concat(OptionalTraits);

        public override string ToString() => $"{Pal.Name} with {RequiredTraits.TraitsListToString()}";

        public bool IsSatisfiedBy(IPalReference palRef) => Pal == palRef.Pal && !RequiredTraits.Except(palRef.EffectiveTraits).Any();

        public void Normalize()
        {
            RequiredTraits = RequiredTraits.Distinct().ToList();
            OptionalTraits = OptionalTraits.Except(RequiredTraits).Distinct().ToList();
        }
    }
}
