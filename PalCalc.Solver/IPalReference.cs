using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public interface IPalReference
    {
        Pal Pal { get; }
        List<Trait> Traits { get; }
        int TraitsHash { get; } // optimization

        PalGender Gender { get; }

        int NumTotalBreedingSteps { get; }

        string TraitsString => Traits.TraitsListToString();

        IPalRefLocation Location { get; }

        TimeSpan BreedingEffort { get; }
        TimeSpan SelfBreedingEffort { get; }

        IPalReference WithGuaranteedGender(PalDB db, PalGender gender);

        bool IsCompatibleGender(PalGender otherGender) => Gender == PalGender.WILDCARD || Gender != otherGender;
    }

    public class OwnedPalReference : IPalReference
    {
        PalInstance instance;

        public OwnedPalReference(PalInstance instance)
        {
            this.instance = instance;

            TraitsHash = instance.Traits.SetHash();
        }

        public PalInstance UnderlyingInstance => instance;

        public Pal Pal => instance.Pal;

        public List<Trait> Traits => instance.Traits;

        public int TraitsHash { get; }

        public PalGender Gender => instance.Gender;

        public IPalRefLocation Location => new OwnedRefLocation() { OwnerId = instance.OwnerPlayerId, Location = instance.Location };

        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;

        public int NumTotalBreedingSteps => 0;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (gender != this.Gender) throw new Exception("Cannot force a gender change for owned pals");
            return this;
        }

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({Traits.TraitsListToString()}) in {Location}";
    }

    public class WildPalReference : IPalReference
    {
        public WildPalReference(Pal pal, int numTraits)
        {
            Pal = pal;
            SelfBreedingEffort = GameConstants.TimeToCatch(pal) / GameConstants.TraitWildAtMostN[numTraits];
            Traits = Enumerable.Range(0, numTraits).Select(i => new RandomTrait()).ToList<Trait>();
            Gender = PalGender.WILDCARD;

            TraitsHash = Traits.SetHash();
        }

        private WildPalReference(Pal pal)
        {
            Pal = pal;
        }

        public Pal Pal { get; private set; }

        public List<Trait> Traits { get; private set; }

        public PalGender Gender { get; private set; }

        public IPalRefLocation Location { get; } = new CapturedRefLocation();

        public TimeSpan BreedingEffort => SelfBreedingEffort * CapturesRequiredForGender;

        // est. number of captured pals required to get a pal of the given gender (assuming you caught every
        // wild pal without checking for gender, not realistic but good enough)
        public int CapturesRequiredForGender
        {
            // assuming 50/50 chance of a wild instance of this pal to have either gender, in which case
            // you'd need on avg. two captures to get the target gender
            get => Gender == PalGender.WILDCARD ? 1 : 2;
        }

        // used as the effort required to catch one
        public TimeSpan SelfBreedingEffort { get; private set; }

        public int NumTotalBreedingSteps => 0;

        public int TraitsHash { get; }

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (Gender != PalGender.WILDCARD) throw new Exception("Wild pal has already been given a guaranteed gender");

            if (gender == PalGender.WILDCARD) return this;

            return new WildPalReference(Pal)
            {
                SelfBreedingEffort = SelfBreedingEffort,
                Gender = gender,
                Traits = Traits
            };
        }

        public override string ToString() => $"Captured {Gender} {Pal} w/ up to {Traits.Count} random traits";
    }

    public class BredPalReference : IPalReference
    {
        private GameSettings gameSettings;

        private BredPalReference(GameSettings gameSettings, Pal pal, IPalReference parent1, IPalReference parent2, List<Trait> traits)
        {
            this.gameSettings = gameSettings;

            Pal = pal;
            if (parent1.Pal.InternalIndex > parent2.Pal.InternalIndex)
            {
                Parent1 = parent1;
                Parent2 = parent2;
            }
            else
            {
                Parent1 = parent2;
                Parent2 = parent1;
            }
            Traits = traits;
            TraitsHash = traits.SetHash();
        }

        public BredPalReference(GameSettings gameSettings, Pal pal, IPalReference parent1, IPalReference parent2, List<Trait> traits, float traitsProbability) : this(gameSettings, pal, parent1, parent2, traits)
        {
            Gender = PalGender.WILDCARD;
            if (traitsProbability <= 0) AvgRequiredBreedings = int.MaxValue;
            else AvgRequiredBreedings = (int)Math.Ceiling(1.0f / traitsProbability);

            TraitsProbability = traitsProbability;
        }

        public float TraitsProbability { get; private set; }

        public Pal Pal { get; private set; }
        public IPalReference Parent1 { get; private set; }
        public IPalReference Parent2 { get; private set; }

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public IPalRefLocation Location => BredRefLocation.Instance;

        public int AvgRequiredBreedings { get; private set; }
        public TimeSpan SelfBreedingEffort => AvgRequiredBreedings * gameSettings.AvgBreedingTime;
        public TimeSpan BreedingEffort => SelfBreedingEffort + (
            gameSettings.MultipleBreedingFarms && Parent1 is BredPalReference && Parent2 is BredPalReference
                ? Parent1.BreedingEffort > Parent2.BreedingEffort
                    ? Parent1.BreedingEffort
                    : Parent2.BreedingEffort
                : Parent1.BreedingEffort + Parent2.BreedingEffort

        );

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

        public List<Trait> Traits { get; }

        public int TraitsHash { get; }

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (this.Gender != PalGender.WILDCARD) throw new Exception("Cannot change gender of bred pal with an already-guaranteed gender");

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
                    return new BredPalReference(gameSettings, Pal, Parent1, Parent2, Traits)
                    {
                        AvgRequiredBreedings = (int)Math.Ceiling(AvgRequiredBreedings/ db.BreedingGenderProbability[Pal][db.BreedingLeastLikelyGender[Pal]]),
                        Gender = gender,
                        TraitsProbability = TraitsProbability,
                    };
                }

                // no preferred bred gender, i.e. 50/50 bred chance, so have half the probability / twice the effort to get desired instance
                return new BredPalReference(gameSettings, Pal, Parent1, Parent2, Traits)
                {
                    AvgRequiredBreedings = AvgRequiredBreedings * 2,
                    Gender = gender,
                    TraitsProbability = TraitsProbability,
                };
            }
            else
            {
                var genderProbability = db.BreedingGenderProbability[Pal][gender];
                return new BredPalReference(gameSettings, Pal, Parent1, Parent2, Traits)
                {
                    AvgRequiredBreedings = (int)Math.Ceiling(AvgRequiredBreedings / genderProbability),
                    Gender = gender,
                    TraitsProbability = TraitsProbability,
                };
            }
        }

        public override string ToString() => $"Bred {Gender} {Pal} w/ ({Traits.TraitsListToString()})";

        public override bool Equals(object obj)
        {
            var asBred = obj as BredPalReference;
            if (ReferenceEquals(asBred, null)) return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode() => HashCode.Combine(
            Pal,
            Parent1.GetHashCode() ^ Parent2.GetHashCode(),
            TraitsHash,
            SelfBreedingEffort,
            Gender
        );
    }
}
