using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
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
            FPassiveSet passives,
            FIVSet ivs
        )
        {
            this.gameSettings = gameSettings;

            Pal = pal;
            if (parent1.Pal.InternalIndex > parent2.Pal.InternalIndex)
            {
                Parent1 = parent1;
                Parent2 = parent2;
            }
            else if (parent1.Pal.InternalIndex < parent2.Pal.InternalIndex)
            {
                Parent1 = parent2;
                Parent2 = parent1;
            }
            else if (parent1.GetHashCode() < parent2.GetHashCode())
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

            EffectivePassives = passives;

            parentBreedingEffort = gameSettings.MultipleBreedingFarms && Parent1 is BredPalReference && Parent2 is BredPalReference
                ? Parent1.BreedingEffort > Parent2.BreedingEffort
                    ? Parent1.BreedingEffort
                    : Parent2.BreedingEffort
                : Parent1.BreedingEffort + Parent2.BreedingEffort;

            TimeFactor = EffectivePassives.ModelObjects.ToTimeFactor();
        }

        public BredPalReference(
            GameSettings gameSettings,
            Pal pal,
            IPalReference parent1,
            IPalReference parent2,
            FPassiveSet passives,
            float passivesProbability,
            FIVSet ivs,
            float ivsProbability
        ) : this(gameSettings, pal, parent1, parent2, passives, ivs)
        {
            Gender = PalGender.WILDCARD;
            if (passivesProbability <= 0 || ivsProbability <= 0)
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

        public FIVSet IVs { get; private set; }
        public float IVsProbability { get; private set; }

        public float TimeFactor { get; }

        private int _avgRequiredBreedings;
        public int AvgRequiredBreedings
        {
            get => _avgRequiredBreedings;
            set
            {
                _avgRequiredBreedings = value;

                var timePerBreed = gameSettings.AvgBreedingTime * Parent1.TimeFactor * Parent2.TimeFactor;
                var totalBreedingTime = _avgRequiredBreedings * timePerBreed;

                var incubationTime = Pal.EggSize.IncubationTime(gameSettings);
                var totalIncubationTime = _avgRequiredBreedings * incubationTime;

                if (gameSettings.MultipleIncubators)
                {
                    // time to get the desired pal is just time to produce the egg + time to incubate it
                    SelfBreedingEffort = totalBreedingTime + incubationTime;
                }
                else
                {
                    // either breeding time will outweigh incubation time, or vice-versa. regardless of which part
                    // is the bottleneck, we'll always need to do the other part at least once.
                    //
                    // (though, realistically, incubation will always take longer than breeding, unless incubation
                    // time is turned off entirely)

                    var allIncubationWithBreeding = totalIncubationTime + timePerBreed;
                    var allBreedingWithIncubation = totalBreedingTime + incubationTime;

                    if (allIncubationWithBreeding > allBreedingWithIncubation)
                        SelfBreedingEffort = allIncubationWithBreeding;
                    else
                        SelfBreedingEffort = allBreedingWithIncubation;
                }
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

        public FPassiveSet EffectivePassives { get; }
        public FPassiveSet ActualPassives => EffectivePassives;

        private BredPalReference WithGuaranteedGenderImpl(PalDB db, PalGender gender)
        {
            if (gender == PalGender.WILDCARD)
                return this;           

            int newBreedings;

            if (gender == PalGender.OPPOSITE_WILDCARD)
            {
                // should only happen if the other parent has the same gender probabilities as this parent
                if (db.BreedingMostLikelyGender[Pal] != PalGender.WILDCARD)
                {
                    // assume that the other parent has the more likely gender
                    newBreedings = (int)Math.Ceiling(AvgRequiredBreedings / db.BreedingGenderProbability[Pal][db.BreedingLeastLikelyGender[Pal]]);
                }
                else
                {
                    // no preferred bred gender, i.e. 50/50 bred chance, so have half the probability / twice the effort to get desired instance
                    newBreedings = AvgRequiredBreedings * 2;
                }
            }
            else
            {
                newBreedings = (int)Math.Ceiling(AvgRequiredBreedings / db.BreedingGenderProbability[Pal][gender]);
            }

            return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectivePassives, IVs)
            {
                AvgRequiredBreedings = newBreedings,
                Gender = gender,
                PassivesProbability = PassivesProbability,
                IVsProbability = IVsProbability,
            };
        }

        private IPalReference cachedOppositeWildcardRef;
        private IPalReference cachedMaleRef;
        private IPalReference cachedFemaleRef;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (Gender != PalGender.WILDCARD) throw new Exception("Cannot change gender of bred pal with an already-guaranteed gender");

            switch (gender)
            {
                case PalGender.WILDCARD: return this;
                case PalGender.OPPOSITE_WILDCARD: return cachedOppositeWildcardRef ??= WithGuaranteedGenderImpl(db, gender);
                case PalGender.MALE: return cachedMaleRef ??= WithGuaranteedGenderImpl(db, gender);
                case PalGender.FEMALE: return cachedFemaleRef ??= WithGuaranteedGenderImpl(db, gender);
                default: throw new NotImplementedException();
            }
        }

        public override string ToString() => $"Bred {Gender} {Pal} w/ ({EffectivePassives.ModelObjects.PassiveSkillListToString()})";

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
            EffectivePassives,
            BreedingEffort,
            SelfBreedingEffort,
            Gender,
            IVs
        );
    }
}
