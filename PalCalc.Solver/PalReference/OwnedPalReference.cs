using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    public class OwnedPalReference : IPalReference
    {
        PalInstance instance;

        /// <param name="effectivePassives">The list of passives held by the `instance`, filtered/re-mapped based on desired passives. (.ToDedicatedPassives())</param>
        public OwnedPalReference(PalDB db, PalInstance instance, List<PassiveSkill> effectivePassives, FIVSet effectiveIVs)
        {
            this.instance = instance;

            TimeFactor = effectivePassives.ToTimeFactor();

            EffectivePassives = FPassiveSet.FromModel(db, effectivePassives);
            ActualPassives = FPassiveSet.FromModel(db, instance.PassiveSkills);

            IVs = effectiveIVs;
        }

        public PalInstance UnderlyingInstance => instance;

        public Pal Pal => instance.Pal;

        public FPassiveSet EffectivePassives { get; private set; }
        public FPassiveSet ActualPassives { get; }

        public float TimeFactor { get; }

        public FIVSet IVs { get; }

        public PalGender Gender => instance.Gender;

        public IPalRefLocation Location => new OwnedRefLocation() { OwnerId = instance.OwnerPlayerId, Location = instance.Location };

        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;

        public int NumTotalBreedingSteps => 0;

        public int NumTotalEggs => 0;

        public int NumTotalWildPals => 0;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (gender != Gender) throw new Exception("Cannot force a gender change for owned pals");
            return this;
        }

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({EffectivePassives.ModelObjects.PassiveSkillListToString()}) in {Location}";

        public override int GetHashCode() => HashCode.Combine(nameof(OwnedPalReference), UnderlyingInstance.GetHashCode());
    }
}
