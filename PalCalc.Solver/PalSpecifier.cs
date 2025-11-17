using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
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
        public FPassiveSet RequiredPassives { get; set; } = FPassiveSet.Empty;
        public PalGender RequiredGender { get; set; } = PalGender.WILDCARD;

        public FPassiveSet OptionalPassives { get; set; } = FPassiveSet.Empty;

        public FPassiveSet DesiredPassives => RequiredPassives.Concat(OptionalPassives);

        public int IV_HP { get; set; }
        public int IV_Attack { get; set; }
        public int IV_Defense { get; set; }

        public override string ToString() => $"{Pal.Name} with {RequiredPassives.ModelObjects.PassiveSkillListToString()}";

        public bool IsSatisfiedBy(IPalReference palRef) =>
            Pal == palRef.Pal &&
            RequiredPassives.Except(palRef.EffectivePassives).IsEmpty &&
            (RequiredGender == PalGender.WILDCARD || palRef.Gender == PalGender.WILDCARD || palRef.Gender == RequiredGender) &&
            (IV_HP == 0 || palRef.IVs.HP.Satisfies(IV_HP)) &&
            (IV_Attack == 0 || palRef.IVs.Attack.Satisfies(IV_Attack)) &&
            (IV_Defense == 0 || palRef.IVs.Defense.Satisfies(IV_Defense));

        public void Normalize()
        {
            OptionalPassives = OptionalPassives.Except(RequiredPassives);
        }
    }
}
