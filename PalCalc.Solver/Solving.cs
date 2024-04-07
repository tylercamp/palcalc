using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    interface IPalLocation { }
    class OwnedPalLocation : IPalLocation
    {
        public PalLocation Location { get; set; }

        public override string ToString() => Location.ToString();
    }

    class CapturedPal : IPalLocation
    {
        public override string ToString() => "(Wild)";

        public static IPalLocation Instance { get; } = new CapturedPal();
    }

    class BredPal : IPalLocation
    {
        public override string ToString() => "(Bred)";

        public static IPalLocation Instance { get; } = new BredPal();
    }

    interface IPalReference
    {
        Pal Pal { get; }
        List<Trait> Traits { get; }
        PalGender Gender { get; }

        IPalLocation Location { get; }

        public TimeSpan BreedingEffort { get; }
        public TimeSpan SelfBreedingEffort { get; }

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender);

        bool IsCompatibleGender(PalGender otherGender) => Gender == PalGender.WILDCARD || Gender != otherGender;
    }

    class OwnedPalReference : IPalReference
    {
        PalInstance instance;

        public OwnedPalReference(PalInstance instance)
        {
            this.instance = instance;
        }

        public Pal Pal => instance.Pal;

        public List<Trait> Traits => instance.Traits;

        public PalGender Gender => instance.Gender;

        public IPalLocation Location => new OwnedPalLocation() { Location = instance.Location };

        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (gender != this.Gender) throw new Exception("Cannot force a gender change for owned pals");
            return this;
        }

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({Traits.TraitsListToString()}) in {Location}";
    }

    class WildcardPalReference : IPalReference
    {
        public WildcardPalReference(Pal pal, int numTraits)
        {
            Pal = pal;
            SelfBreedingEffort = GameConfig.TimeToCatch(pal) / GameConfig.TraitWildAtMostN[numTraits];
            Traits = Enumerable.Range(0, numTraits).Select(i => new RandomTrait()).ToList<Trait>();
            Gender = PalGender.WILDCARD;
        }

        private WildcardPalReference(Pal pal)
        {
            Pal = pal;
        }

        public Pal Pal { get; private set; }

        public List<Trait> Traits { get; private set; }

        public PalGender Gender { get; private set; }

        public IPalLocation Location { get; } = new CapturedPal();

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

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (Gender != PalGender.WILDCARD) throw new Exception("Wild pal has already been given a guaranteed gender");

            if (gender == PalGender.WILDCARD) return this;

            return new WildcardPalReference(Pal)
            {
                SelfBreedingEffort = SelfBreedingEffort,
                Gender = gender,
                Traits = Traits
            };
        }

        public override string ToString() => $"Captured {Gender} {Pal} w/ up to {Traits.Count} random traits";
    }

    class BredPalReference : IPalReference
    {
        private BredPalReference(Pal pal, IPalReference parent1, IPalReference parent2, List<Trait> traits)
        {
            // TODO - if both parents are wildcards, let the parent with the least effort have its base
            //        effort, and only the other (least effort) parent would get the penalty of having to
            //        get a specific gender (need to take gender probabilities into account though)

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
        }

        public BredPalReference(Pal pal, IPalReference parent1, IPalReference parent2, List<Trait> traits, float traitsProbability) : this(pal, parent1, parent2, traits)
        {
            Gender = PalGender.WILDCARD;
            if (traitsProbability <= 0) AvgRequiredBreedings = int.MaxValue;
            else AvgRequiredBreedings = (int)Math.Ceiling(1.0f / traitsProbability);
        }

        public Pal Pal { get; private set; }
        public IPalReference Parent1 { get; private set; }
        public IPalReference Parent2 { get; private set; }

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public IPalLocation Location => BredPal.Instance;

        public int AvgRequiredBreedings { get; private set; }
        public TimeSpan SelfBreedingEffort => AvgRequiredBreedings * GameConfig.AvgBreedingTime;
        public TimeSpan BreedingEffort => SelfBreedingEffort + (
            GameConfig.MultipleBreedingFarms
                ? Parent1.BreedingEffort > Parent2.BreedingEffort
                    ? Parent1.BreedingEffort
                    : Parent2.BreedingEffort
                : Parent1.BreedingEffort + Parent2.BreedingEffort

        );

        public List<Trait> Traits { get; }

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
                    return new BredPalReference(Pal, Parent1, Parent2, Traits)
                    {
                        AvgRequiredBreedings = (int)Math.Ceiling(AvgRequiredBreedings/ db.BreedingGenderProbability[Pal][db.BreedingLeastLikelyGender[Pal]]),
                        Gender = gender
                    };
                }

                // no preferred bred gender, i.e. 50/50 bred chance, so have half the probability / twice the effort to get desired instance
                return new BredPalReference(Pal, Parent1, Parent2, Traits)
                {
                    AvgRequiredBreedings = AvgRequiredBreedings * 2,
                    Gender = gender
                };
            }
            else
            {
                var genderProbability = db.BreedingGenderProbability[Pal][gender];
                return new BredPalReference(Pal, Parent1, Parent2, Traits)
                {
                    AvgRequiredBreedings = (int)Math.Ceiling(AvgRequiredBreedings / genderProbability),
                    Gender = gender
                };
            }
        }

        public override string ToString() => $"Bred {Gender} {Pal} w/ ({Traits.TraitsListToString()})";

        public override bool Equals(object obj)
        {
            var asBred = obj as BredPalReference;
            if (ReferenceEquals(asBred, null)) return false;

            return (
                asBred.Pal == Pal &&
                asBred.Parent1.Equals(Parent1) &&
                asBred.Parent2.Equals(Parent2) &&
                asBred.SelfBreedingEffort == SelfBreedingEffort &&
                asBred.Gender == Gender &&
                asBred.Traits.EqualsTraits(asBred.Traits)
            );
        }

        public override int GetHashCode() => HashCode.Combine(
            Pal,
            Parent1,
            Parent2,
            Traits,
            SelfBreedingEffort,
            Gender
        );
    }
}
