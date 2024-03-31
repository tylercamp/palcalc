using PalCalc.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
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

        IPalReference EnsureOppositeGender(PalDB db, PalGender gender);
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

        public IPalReference EnsureOppositeGender(PalDB db, PalGender gender) => gender != instance.Gender ? this : null;

        public TimeSpan BreedingEffort => TimeSpan.Zero;

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({string.Join(", ", Traits)}) in {Location}";
    }

    class WildcardPalReference : IPalReference
    {
        public WildcardPalReference(Pal pal)
        {
            Pal = pal;
            BreedingEffort = GameConfig.TimeToCatch(pal);
        }

        public Pal Pal { get; private set; }

        public List<Trait> Traits { get; } = new List<Trait>();

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public IPalLocation Location { get; } = new CapturedPal();

        public IPalReference EnsureOppositeGender(PalDB db, PalGender gender)
        {
            // seems like wild pals just have a flat 50/50 chance on gender, rather than following breeding probability

            if (gender == PalGender.WILDCARD)
            {
                // this method is meant to update the effort for getting a pal of opposing gender, but:
                // - for two wildcard pals, the effort calc should only happen on one of the instances, which we can't ensure here
                // - for a wildcard + definite-gender, the effort on this instance has already been applied, so we'll need to undo
                //   the original offset on this so the effort calc can be ensured-applied-once outside this method

                if (this.Gender == PalGender.WILDCARD) return this;
                else return new WildcardPalReference(Pal);
            }
            else if (this.Gender == PalGender.WILDCARD)
            {
                var newGender = gender == PalGender.MALE ? PalGender.FEMALE : PalGender.MALE;
                return new WildcardPalReference(Pal)
                {
                    Gender = newGender,
                    // assume 50/50 chance of wild pal having a given gender, so on avg. we'd need to catch two
                    BreedingEffort = this.BreedingEffort * 2
                };
            }
            else
            {
                // incompatible (only wildcards can be resolved to new genders)
                return this.Gender != gender ? this : null;
            }
        }

        public TimeSpan BreedingEffort { get; private set; }

        public override string ToString() => $"Captured {Gender} {Pal}";
    }

    class BredPalReference : IPalReference
    {
        private BredPalReference(Pal pal, IPalReference parent1, IPalReference parent2, List<Trait> traits)
        {
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

            var estBreedAttempts = (int)Math.Ceiling(1.0f / traitsProbability);
            SelfBreedingEffort = GameConfig.BreedingTime * estBreedAttempts;
        }

        public Pal Pal { get; private set; }
        public IPalReference Parent1 { get; private set; }
        public IPalReference Parent2 { get; private set; }

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public IPalLocation Location => BredPal.Instance;

        public IPalReference EnsureOppositeGender(PalDB db, PalGender gender)
        {
            if (gender == PalGender.WILDCARD)
            {
                if (this.Gender != PalGender.WILDCARD) return this;
                else
                {
                    // this pal and the other pal can have any gender, but we're asking for a specific
                    // gender on THIS pal. be optimistic and choose the gender that is most likely as a
                    // breeding result
                    var bestProbability = db.BreedingGenderProbability[Pal].MaxBy(kvp => kvp.Value);
                    return new BredPalReference(Pal, Parent1, Parent2, Traits)
                    {
                        Gender = bestProbability.Key,
                        // breeding effort for this specific gender is effort for this pal with traits, times the est. no. of
                        // breeding attempts for that gender
                        SelfBreedingEffort = this.SelfBreedingEffort * Math.Ceiling(1.0f / bestProbability.Value),
                    };
                }
            }
            else
            {
                if (this.Gender != PalGender.WILDCARD) return this.Gender != gender ? this : null;
                else
                {
                    var requiredGender = gender == PalGender.MALE ? PalGender.FEMALE : PalGender.MALE;
                    var genderProbability = db.BreedingGenderProbability[Pal][requiredGender];
                    return new BredPalReference(Pal, Parent1, Parent2, Traits)
                    {
                        Gender = requiredGender,
                        SelfBreedingEffort = this.SelfBreedingEffort * Math.Ceiling(1.0f / genderProbability),
                    };
                }
            }
        }

        public TimeSpan SelfBreedingEffort { get; private set; }
        public TimeSpan BreedingEffort => Parent1.BreedingEffort + Parent2.BreedingEffort + SelfBreedingEffort;

        public List<Trait> Traits { get; }

        public override string ToString() => $"Bred {Gender} {Pal} w/ ({string.Join(", ", Traits)})";

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
                asBred.Traits.SequenceEqual(asBred.Traits)
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
