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

        // TODO: Generalize this singular target to required and optional attack collections
        // when breeding can produce more than one targeted attack. Add the corresponding
        // request serialization and UI selection/display at the same time.
        public ActiveSkill RequiredAttack { get; set; }
        public PalGender RequiredGender { get; set; } = PalGender.WILDCARD;

        public List<PassiveSkill> OptionalPassives { get; set; } = new List<PassiveSkill>();

        public IEnumerable<PassiveSkill> DesiredPassives => RequiredPassives.Concat(OptionalPassives);

        public int IV_HP { get; set; }
        public int IV_Attack { get; set; }
        public int IV_Defense { get; set; }

        public override string ToString() => $"{Pal.Name} with {RequiredPassives.PassiveSkillListToString()}" +
            (RequiredAttack == null ? "" : $" and {RequiredAttack}");

        public bool IsSatisfiedBy(IPalReference palRef) =>
            Pal == palRef.Pal &&
            !RequiredPassives.Except(palRef.EffectivePassives).Any() &&
            (RequiredAttack == null || palRef.EffectiveAttack == RequiredAttack) &&
            (RequiredGender == PalGender.WILDCARD || palRef.Gender == PalGender.WILDCARD || palRef.Gender == RequiredGender) &&
            (IV_HP == 0 || palRef.IVs.HP.Satisfies(IV_HP)) &&
            (IV_Attack == 0 || palRef.IVs.Attack.Satisfies(IV_Attack)) &&
            (IV_Defense == 0 || palRef.IVs.Defense.Satisfies(IV_Defense));

        public void Normalize()
        {
            RequiredPassives = RequiredPassives.Distinct().ToList();
            OptionalPassives = OptionalPassives.Except(RequiredPassives).Distinct().ToList();
        }

        internal PalSpecifier NormalizedCopy()
        {
            var requiredPassives = RequiredPassives.Distinct().ToList();

            return new PalSpecifier
            {
                Pal = Pal,
                RequiredPassives = requiredPassives,
                RequiredAttack = RequiredAttack,
                RequiredGender = RequiredGender,
                OptionalPassives = OptionalPassives
                    .Except(requiredPassives)
                    .Distinct()
                    .ToList(),
                IV_HP = IV_HP,
                IV_Attack = IV_Attack,
                IV_Defense = IV_Defense,
            };
        }
    }
}
