using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public class PalSpecifier
    {
        /// <summary>
        /// Two parents can equip at most three attacks each, so an inherit-all
        /// breeding step can transfer at most six distinct requested attacks.
        /// This also keeps attack profiles representable as one 64-value mask table.
        /// </summary>
        public const int MaxRequiredAttacks = 6;

        public Pal Pal { get; set; }
        public List<PassiveSkill> RequiredPassives { get; set; } = new List<PassiveSkill>();

        public List<ActiveSkill> RequiredAttacks { get; set; } = new List<ActiveSkill>();
        public PalGender RequiredGender { get; set; } = PalGender.WILDCARD;

        public List<PassiveSkill> OptionalPassives { get; set; } = new List<PassiveSkill>();

        public IEnumerable<PassiveSkill> DesiredPassives => RequiredPassives.Concat(OptionalPassives);

        public int IV_HP { get; set; }
        public int IV_Attack { get; set; }
        public int IV_Defense { get; set; }

        public override string ToString() => $"{Pal.Name} with {RequiredPassives.PassiveSkillListToString()}" +
            (RequiredAttacks.Count == 0 ? "" : $" and {string.Join(", ", RequiredAttacks)}");

        private bool PassivesMatchRequirements(List<PassiveSkill> passives)
        {
            // Unrolled `!RequiredPassives.Except(passives).Any()`
            foreach (var p in RequiredPassives)
            {
                if (!passives.Contains(p))
                    return false;
            }

            return true;
        }

        internal bool IsSatisfiedByIgnoringAttacks(IPalReference palRef) =>
            IsSatisfiedByIgnoringAttacks(
                palRef.Pal,
                palRef.Gender,
                palRef.IVs,
                palRef.EffectivePassives
            );

        internal bool IsSatisfiedByIgnoringAttacks(
            Pal pal,
            PalGender gender,
            IV_Set ivs,
            List<PassiveSkill> passives
        ) =>
            Pal == pal &&
            (RequiredGender == PalGender.WILDCARD || gender == PalGender.WILDCARD || gender == RequiredGender) &&
            (IV_HP == 0 || ivs.HP.Satisfies(IV_HP)) &&
            (IV_Attack == 0 || ivs.Attack.Satisfies(IV_Attack)) &&
            (IV_Defense == 0 || ivs.Defense.Satisfies(IV_Defense)) &&
            PassivesMatchRequirements(passives);

        public bool IsSatisfiedBy(IPalReference palRef) =>
            IsSatisfiedByIgnoringAttacks(palRef) &&
            (RequiredAttacks.Count == 0 || palRef.AttackProfile.Contains(
                (byte)((1 << RequiredAttacks.Count) - 1)
            ));

        public void Normalize()
        {
            RequiredPassives = RequiredPassives.Distinct().ToList();
            RequiredAttacks = RequiredAttacks.Distinct().ToList();
            OptionalPassives = OptionalPassives.Except(RequiredPassives).Distinct().ToList();
        }

        internal PalSpecifier NormalizedCopy()
        {
            var requiredPassives = RequiredPassives.Distinct().ToList();

            return new PalSpecifier
            {
                Pal = Pal,
                RequiredPassives = requiredPassives,
                RequiredAttacks = RequiredAttacks.Distinct().ToList(),
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
