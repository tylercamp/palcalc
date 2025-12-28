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

            Gender = instance.Gender;
        }

        public PalInstance UnderlyingInstance => instance;

        public Pal Pal => instance.Pal;

        public FPassiveSet EffectivePassives { get; private set; }
        public FPassiveSet ActualPassives { get; }

        public float TimeFactor { get; }

        public FIVSet IVs { get; }

        // (Make this private-settable for use by WithGuaranteedGender when gender-reversers are enabled)
        public PalGender Gender { get; private set; }

        public IPalRefLocation Location => new OwnedRefLocation() { OwnerId = instance.OwnerPlayerId, Location = instance.Location };

        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;

        public int TotalCost => 0;

        public int NumTotalBreedingSteps => 0;

        public int NumTotalEggs => 0;

        public int NumTotalWildPals => 0;

        private OwnedPalReference cachedFemaleRef, cachedMaleRef, cachedWildcardRef, cachedOppositeWildcardRef;

        private OwnedPalReference MakeGuaranteedGenderImpl(PalDB db, PalGender gender)
        {
            var res = new OwnedPalReference(db, instance, EffectivePassives.ModelObjects.ToList(), IVs);
            res.Gender = gender;
            return res;
        }

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser)
        {
            if (gender != Gender)
            {
                if (!useReverser)
                    throw new Exception("Cannot force a gender change for owned pals without a gender reverser");

                switch (gender)
                {
                    case PalGender.FEMALE: return cachedFemaleRef ??= MakeGuaranteedGenderImpl(db, gender);
                    case PalGender.MALE: return cachedMaleRef ??= MakeGuaranteedGenderImpl(db, gender);
                    case PalGender.OPPOSITE_WILDCARD: return cachedOppositeWildcardRef ??= MakeGuaranteedGenderImpl(db, gender);
                    case PalGender.WILDCARD: return cachedWildcardRef ??= MakeGuaranteedGenderImpl(db, gender);
                }
            }

            return this;
        }

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({EffectivePassives.ModelObjects.PassiveSkillListToString()}) in {Location}";

        public override int GetHashCode() => HashCode.Combine(nameof(OwnedPalReference), UnderlyingInstance.GetHashCode());
    }
}
