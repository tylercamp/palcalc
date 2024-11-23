using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    public class OwnedPalReference : IPalReference
    {
        PalInstance instance;

        /// <param name="effectivePassives">The list of passives held by the `instance`, filtered/re-mapped based on desired passives. (.ToDedicatedPassives())</param>
        public OwnedPalReference(PalInstance instance, List<PassiveSkill> effectivePassives)
        {
            this.instance = instance;

            EffectivePassives = effectivePassives;
            EffectivePassivesHash = EffectivePassives.SetHash();

            ActualPassives = instance.PassiveSkills;
        }

        public PalInstance UnderlyingInstance => instance;

        public Pal Pal => instance.Pal;

        public List<PassiveSkill> EffectivePassives { get; private set; }

        public int EffectivePassivesHash { get; }

        public List<PassiveSkill> ActualPassives { get; }

        public PalGender Gender => instance.Gender;

        public IPalRefLocation Location => new OwnedRefLocation() { OwnerId = instance.OwnerPlayerId, Location = instance.Location };

        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;

        public int NumTotalBreedingSteps => 0;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (gender != Gender) throw new Exception("Cannot force a gender change for owned pals");
            return this;
        }

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({EffectivePassives.PassiveSkillListToString()}) in {Location}";

        public override int GetHashCode() => HashCode.Combine(nameof(OwnedPalReference), UnderlyingInstance.GetHashCode());
    }
}
