using PalCalc.Model;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    public class BredPalReference : IPalReference
    {
        private GameSettings gameSettings;

        private BredPalReference(
            GameSettings gameSettings,
            Pal pal,
            IPalReference parent1,
            IPalReference parent2,
            List<PassiveSkill> passives,
            IV_Set ivs,
            AttackProfile attackProfile,
            bool hasNeutralAttack,
            MaterializedAttackInheritance materializedAttackInheritance
        )
        {
            this.gameSettings = gameSettings;

            Pal = pal;
            var parentOrderReversed = parent1.Pal.InternalIndex > parent2.Pal.InternalIndex ||
                parent1.Pal.InternalIndex == parent2.Pal.InternalIndex && parent1.GetHashCode() < parent2.GetHashCode();
            if (parentOrderReversed)
            {
                Parent1 = parent1;
                Parent2 = parent2;
            }
            else
            {
                Parent1 = parent2;
                Parent2 = parent1;
            }

            IVs = ivs;
            AttackProfile = attackProfile;
            HasNeutralAttack = hasNeutralAttack;
            MaterializedAttackInheritance = parentOrderReversed || materializedAttackInheritance is null
                ? materializedAttackInheritance
                : materializedAttackInheritance with
                {
                    Parent1Loadout = materializedAttackInheritance.Parent2Loadout,
                    Parent2Loadout = materializedAttackInheritance.Parent1Loadout,
                };

            EffectivePassives = passives;
            EffectivePassivesHash = passives.SetHash(p => p.InternalName);

            parentBreedingEffort = gameSettings.MultipleBreedingFarms && Parent1 is BredPalReference && Parent2 is BredPalReference
                ? Parent1.BreedingEffort > Parent2.BreedingEffort
                    ? Parent1.BreedingEffort
                    : Parent2.BreedingEffort
                : Parent1.BreedingEffort + Parent2.BreedingEffort;

            TimeFactor = EffectivePassives.ToTimeFactor();
        }

        public BredPalReference(
            GameSettings gameSettings,
            Pal pal,
            IPalReference parent1,
            IPalReference parent2,
            List<PassiveSkill> passives,
            float passivesProbability,
            IV_Set ivs,
            float ivsProbability,
            AttackProfile attackProfile,
            bool hasNeutralAttack,
            MaterializedAttackInheritance materializedAttackInheritance,
            int? avgRequiredBreedings,
            PalGender gender
        ) : this(gameSettings, pal, parent1, parent2, passives, ivs, attackProfile, hasNeutralAttack, materializedAttackInheritance)
        {
            Gender = gender;
            if (avgRequiredBreedings is int materializedBreedings)
            {
                AvgRequiredBreedings = materializedBreedings;
            }
            else if (passivesProbability <= 0 || ivsProbability <= 0)
            {
                // don't think this is actually needed anymore, keeping just in case
#if DEBUG
                Debugger.Break();
#endif
                AvgRequiredBreedings = int.MaxValue;
            }
            else AvgRequiredBreedings = (int)Math.Ceiling(1.0f / (passivesProbability * ivsProbability));

            PassivesProbability = passivesProbability;
            IVsProbability = ivsProbability;
        }

        public float PassivesProbability { get; private set; }

        public Pal Pal { get; private set; }
        public IPalReference Parent1 { get; private set; }
        public IPalReference Parent2 { get; private set; }

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public IPalRefLocation Location => BredRefLocation.Instance;

        public IV_Set IVs { get; private set; }
        public float IVsProbability { get; private set; }

        public float TimeFactor { get; }

        private int _avgRequiredBreedings;
        public int AvgRequiredBreedings
        {
            get => _avgRequiredBreedings;
            set
            {
                _avgRequiredBreedings = value;
                SelfBreedingEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
                    gameSettings, Pal, Parent1.TimeFactor, Parent2.TimeFactor, _avgRequiredBreedings
                );
            }
        }

        private TimeSpan _selfBreedingEffort;
        public TimeSpan SelfBreedingEffort
        {
            get => _selfBreedingEffort;
            private set
            {
                _selfBreedingEffort = value;
                BreedingEffort = _selfBreedingEffort + parentBreedingEffort;
            }
        }

        public int TotalCost => Parent1.TotalCost + Parent2.TotalCost;

        private TimeSpan parentBreedingEffort;
        public TimeSpan BreedingEffort { get; private set; }

        private int numTotalBreedingSteps = -1;
        public int NumTotalBreedingSteps
        {
            get
            {
                if (numTotalBreedingSteps < 0)
                    numTotalBreedingSteps = 1 + Parent1.NumTotalBreedingSteps + Parent2.NumTotalBreedingSteps;

                return numTotalBreedingSteps;
            }
        }

        public int NumTotalEggs => AvgRequiredBreedings + Parent1.NumTotalEggs + Parent2.NumTotalEggs;

        public int NumTotalWildPals => Parent1.NumTotalWildPals + Parent2.NumTotalWildPals;

        public List<PassiveSkill> EffectivePassives { get; }

        public int EffectivePassivesHash { get; }

        public List<PassiveSkill> ActualPassives => EffectivePassives;

        public AttackProfile AttackProfile { get; }

        public bool HasNeutralAttack { get; }

        public MaterializedAttackInheritance MaterializedAttackInheritance { get; }

        public bool IsOutdated { get; set; }

        private BredPalReference WithGuaranteedGenderImpl(PalDB db, PalGender gender, bool useReverser)
        {
            if (gender == PalGender.WILDCARD)
            {
                return this;
            }
            else
            {
                return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectivePassives, IVs,
                    AttackProfile.WithGuaranteedGender(gameSettings, Pal, Parent1.TimeFactor, Parent2.TimeFactor, db, gender, useReverser), HasNeutralAttack,
                    MaterializedAttackInheritance)
                {
                    AvgRequiredBreedings = BredPalReferenceEffort.WithGuaranteedGender(AvgRequiredBreedings, Pal, db, gender, useReverser),
                    Gender = gender,
                    PassivesProbability = PassivesProbability,
                    IVsProbability = IVsProbability,
                };
            }
        }

        private IPalReference cachedOppositeWildcardRef;
        private IPalReference cachedMaleRef;
        private IPalReference cachedFemaleRef;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser)
        {
            // this exception isn't really necessary, we'd be okay without it, but we should only expect this to be called on
            // bred pals in the outer pool which don't have a requested gender. these specific-gender pals should only be used
            // as specialized parents of new pals. if these make it back into the broader frontier, there's likely a bug elsewhere
            if (Gender != PalGender.WILDCARD) throw new Exception("A bred pal with already-guaranteed gender should not be asked to change its gender again");

            switch (gender)
            {
                case PalGender.WILDCARD: return this;
                case PalGender.OPPOSITE_WILDCARD: return cachedOppositeWildcardRef ??= WithGuaranteedGenderImpl(db, gender, useReverser);
                case PalGender.MALE: return cachedMaleRef ??= WithGuaranteedGenderImpl(db, gender, useReverser);
                case PalGender.FEMALE: return cachedFemaleRef ??= WithGuaranteedGenderImpl(db, gender, useReverser);
                default: throw new NotImplementedException();
            }
        }

        public override string ToString() => $"Bred {Gender} {Pal} w/ ({EffectivePassives.PassiveSkillListToString()})";

        public override bool Equals(object obj)
        {
            var asBred = obj as BredPalReference;
            if (ReferenceEquals(asBred, null)) return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode() => HashCode.Combine(
            nameof(BredPalReference),
            Pal,
            Parent1.GetHashCode() ^ Parent2.GetHashCode(),
            EffectivePassivesHash,
            BreedingEffort,
            SelfBreedingEffort,
            Gender,
            IVs
        );
    }
}
