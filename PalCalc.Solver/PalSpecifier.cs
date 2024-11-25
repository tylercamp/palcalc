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
        public List<PassiveSkill> RequiredPassives { get; set; } = new List<PassiveSkill>();
        public PalGender RequiredGender { get; set; } = PalGender.WILDCARD;

        public List<PassiveSkill> OptionalPassives { get; set; } = new List<PassiveSkill>();

        public IEnumerable<PassiveSkill> DesiredPassives => RequiredPassives.Concat(OptionalPassives);

        public override string ToString() => $"{Pal.Name} with {RequiredPassives.PassiveSkillListToString()}";

        public bool IsSatisfiedBy(IPalReference palRef) =>
            Pal == palRef.Pal &&
            !RequiredPassives.Except(palRef.EffectivePassives).Any() &&
            (RequiredGender == PalGender.WILDCARD || palRef.Gender == PalGender.WILDCARD || palRef.Gender == RequiredGender);

        public void Normalize()
        {
            RequiredPassives = RequiredPassives.Distinct().ToList();
            OptionalPassives = OptionalPassives.Except(RequiredPassives).Distinct().ToList();
        }
    }
}
