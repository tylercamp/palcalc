using PalCalc.Model;
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
            IV_Set ivs
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

        private BredPalReference WithGuaranteedGenderImpl(PalDB db, PalGender gender, bool useReverser)
        {
            if (gender == PalGender.WILDCARD)
            {
                return this;
            }
            else if (gender == PalGender.OPPOSITE_WILDCARD)
            {
                // should only happen if the other parent has the same gender probabilities as this parent
                if (db.BreedingMostLikelyGender[Pal] != PalGender.WILDCARD)
                {
                    // assume that the other parent has the more likely gender
                    return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectivePassives, IVs)
                    {
                        AvgRequiredBreedings = useReverser ? AvgRequiredBreedings : (int)Math.Ceiling(AvgRequiredBreedings / db.BreedingGenderProbability[Pal][db.BreedingLeastLikelyGender[Pal]]),
                        Gender = gender,
                        PassivesProbability = PassivesProbability,
                        IVsProbability = IVsProbability,
                    };
                }
                else
                {
                    // no preferred bred gender, i.e. 50/50 bred chance, so have half the probability / twice the effort to get desired instance
                    return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectivePassives, IVs)
                    {
                        AvgRequiredBreedings = useReverser ? AvgRequiredBreedings : AvgRequiredBreedings * 2,
                        Gender = gender,
                        PassivesProbability = PassivesProbability,
                        IVsProbability = IVsProbability,
                    };
                }
            }
            else
            {
                var genderProbability = db.BreedingGenderProbability[Pal][gender];
                return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectivePassives, IVs)
                {
                    AvgRequiredBreedings = useReverser ? AvgRequiredBreedings : (int)Math.Ceiling(AvgRequiredBreedings / genderProbability),
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
            // as specialized parents of new pals. if these make it back into the broader working set, there's likely a bug elsewhere
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
