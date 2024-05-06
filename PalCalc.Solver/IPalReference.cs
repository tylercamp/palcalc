using PalCalc.Model;
using System;
using System.Collections.Concurrent;
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

        /// <summary>
        /// The list of DESIRED traits held by this pal. Any irrelevant traits are to
        /// be represented as a Random trait.
        /// </summary>
        List<Trait> EffectiveTraits { get; }
        int EffectiveTraitsHash { get; } // optimization

        List<Trait> ActualTraits { get; }

        PalGender Gender { get; }

        int NumTotalBreedingSteps { get; }

        string EffectiveTraitsString => EffectiveTraits.TraitsListToString();

        IPalRefLocation Location { get; }

        TimeSpan BreedingEffort { get; }
        TimeSpan SelfBreedingEffort { get; }

        IPalReference WithGuaranteedGender(PalDB db, PalGender gender);

        bool IsCompatibleGender(PalGender otherGender) => Gender == PalGender.WILDCARD || Gender != otherGender;
    }

    public class OwnedPalReference : IPalReference
    {
        PalInstance instance;

        /// <param name="effectiveTraits">The list of traits held by the `instance`, filtered/re-mapped based on desired traits. (.ToDedicatedTraits())</param>
        public OwnedPalReference(PalInstance instance, List<Trait> effectiveTraits)
        {
            this.instance = instance;

            EffectiveTraits = effectiveTraits;
            EffectiveTraitsHash = EffectiveTraits.SetHash();

            ActualTraits = instance.Traits;
        }

        public PalInstance UnderlyingInstance => instance;

        public Pal Pal => instance.Pal;

        public List<Trait> EffectiveTraits { get; private set; }

        public int EffectiveTraitsHash { get; }

        public List<Trait> ActualTraits { get; }

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

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({EffectiveTraits.TraitsListToString()}) in {Location}";
    }

    /// <summary>
    /// Represents a pair of male and female instances of the same pal. This allows us to represent
    /// Male+Female owned pals to act as "wildcard" genders. (Without this the solver will tend to prefer
    /// redundantly breeding a pal of "opposite gender" compared to another pal step which has lots of
    /// requirements + breeding attempts. It wouldn't directly pair it with a male or female pal, since
    /// that would require breeding the "difficult" pal to have a specific gender.)
    /// 
    /// These pals _should_, but are not _guaranteed_, to have the same set of traits:
    /// 
    /// - If two pals have different desired traits, they should NOT be made composite.
    /// - Conversely, if one pal has a desired trait, both pals will have that desired trait.
    /// - The traits for this reference will match whichever pal has the most traits.
    /// </summary>
    public class CompositeOwnedPalReference : IPalReference
    {
        public CompositeOwnedPalReference(OwnedPalReference male, OwnedPalReference female)
        {
            Male = male;
            Female = female;

            Location = new CompositeRefLocation(male.Location, female.Location);

            // effective traits based on which pal has the most irrelevant traits
            EffectiveTraits = male.EffectiveTraits.Count > female.EffectiveTraits.Count ? male.EffectiveTraits : female.EffectiveTraits;
            EffectiveTraitsHash = EffectiveTraits.SetHash();

            ActualTraits = Male.ActualTraits.Intersect(Female.ActualTraits).ToList();
            while (ActualTraits.Count < EffectiveTraits.Count) ActualTraits.Add(new RandomTrait());
        }

        public OwnedPalReference Male { get; }
        public OwnedPalReference Female { get; }

        public Pal Pal => Male.Pal;

        public List<Trait> EffectiveTraits { get; private set; }

        public int EffectiveTraitsHash { get; private set; }

        public List<Trait> ActualTraits { get; }

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public int NumTotalBreedingSteps { get; } = 0;

        public IPalRefLocation Location { get; }

        public TimeSpan BreedingEffort { get; } = TimeSpan.Zero;

        public TimeSpan SelfBreedingEffort { get; } = TimeSpan.Zero;

        private CompositeOwnedPalReference oppositeWildcardReference;
        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            switch (gender)
            {
                case PalGender.MALE: return Male;
                case PalGender.FEMALE: return Female;
                case PalGender.WILDCARD: return this;
                case PalGender.OPPOSITE_WILDCARD:
                    if (oppositeWildcardReference == null)
                        oppositeWildcardReference = new CompositeOwnedPalReference(Male, Female) { Gender = gender };
                    return oppositeWildcardReference;

                default: throw new NotImplementedException();
            }
        }
    }

    public class WildPalReference : IPalReference
    {
        public WildPalReference(Pal pal, int numTraits)
        {
            Pal = pal;
            SelfBreedingEffort = GameConstants.TimeToCatch(pal) / GameConstants.TraitWildAtMostN[numTraits];
            EffectiveTraits = Enumerable.Range(0, numTraits).Select(i => new RandomTrait()).ToList<Trait>();
            Gender = PalGender.WILDCARD;

            EffectiveTraitsHash = EffectiveTraits.SetHash();
        }

        private WildPalReference(Pal pal)
        {
            Pal = pal;
        }

        public Pal Pal { get; private set; }

        public List<Trait> EffectiveTraits { get; private set; }

        public PalGender Gender { get; private set; }

        public List<Trait> ActualTraits => EffectiveTraits;

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

        public int EffectiveTraitsHash { get; }

        private IPalReference WithGuaranteedGenderImpl(PalDB db, PalGender gender)
        {
            return new WildPalReference(Pal)
            {
                SelfBreedingEffort = SelfBreedingEffort,
                Gender = gender,
                EffectiveTraits = EffectiveTraits
            };
        }

        private Dictionary<PalGender, IPalReference> cachedGuaranteedGenders = null;
        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (Gender != PalGender.WILDCARD) throw new Exception("Wild pal has already been given a guaranteed gender");

            if (gender == PalGender.WILDCARD) return this;

            if (cachedGuaranteedGenders == null)
            {
                cachedGuaranteedGenders = new List<PalGender>()
                {
                    PalGender.MALE,
                    PalGender.FEMALE,
                    PalGender.OPPOSITE_WILDCARD
                }.ToDictionary(g => g, g => WithGuaranteedGenderImpl(db, g));
            }

            return cachedGuaranteedGenders[gender];
        }

        public override string ToString() => $"Captured {Gender} {Pal} w/ up to {EffectiveTraits.Count} random traits";
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
            EffectiveTraits = traits;
            EffectiveTraitsHash = traits.SetHash();

            parentBreedingEffort = gameSettings.MultipleBreedingFarms && Parent1 is BredPalReference && Parent2 is BredPalReference
                ? Parent1.BreedingEffort > Parent2.BreedingEffort
                    ? Parent1.BreedingEffort
                    : Parent2.BreedingEffort
                : Parent1.BreedingEffort + Parent2.BreedingEffort;
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

        private TimeSpan parentBreedingEffort;
        public TimeSpan BreedingEffort => SelfBreedingEffort + parentBreedingEffort;

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

        public List<Trait> EffectiveTraits { get; }

        public int EffectiveTraitsHash { get; }

        public List<Trait> ActualTraits => EffectiveTraits;

        private IPalReference WithGuaranteedGenderImpl(PalDB db, PalGender gender)
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
                    return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectiveTraits)
                    {
                        AvgRequiredBreedings = (int)Math.Ceiling(AvgRequiredBreedings / db.BreedingGenderProbability[Pal][db.BreedingLeastLikelyGender[Pal]]),
                        Gender = gender,
                        TraitsProbability = TraitsProbability,
                    };
                }
                else
                {
                    // no preferred bred gender, i.e. 50/50 bred chance, so have half the probability / twice the effort to get desired instance
                    return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectiveTraits)
                    {
                        AvgRequiredBreedings = AvgRequiredBreedings * 2,
                        Gender = gender,
                        TraitsProbability = TraitsProbability,
                    };
                }
            }
            else
            {
                var genderProbability = db.BreedingGenderProbability[Pal][gender];
                return new BredPalReference(gameSettings, Pal, Parent1, Parent2, EffectiveTraits)
                {
                    AvgRequiredBreedings = (int)Math.Ceiling(AvgRequiredBreedings / genderProbability),
                    Gender = gender,
                    TraitsProbability = TraitsProbability,
                };
            }
        }

        private ConcurrentDictionary<PalGender, IPalReference> cachedGuaranteedGenders = null;
        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            if (this.Gender != PalGender.WILDCARD) throw new Exception("Cannot change gender of bred pal with an already-guaranteed gender");

            if (cachedGuaranteedGenders == null) cachedGuaranteedGenders = new ConcurrentDictionary<PalGender, IPalReference>();

            return cachedGuaranteedGenders.GetOrAdd(gender, (gender) => WithGuaranteedGenderImpl(db, gender));            
        }

        public override string ToString() => $"Bred {Gender} {Pal} w/ ({EffectiveTraits.TraitsListToString()})";

        public override bool Equals(object obj)
        {
            var asBred = obj as BredPalReference;
            if (ReferenceEquals(asBred, null)) return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode() => HashCode.Combine(
            Pal,
            Parent1.GetHashCode() ^ Parent2.GetHashCode(),
            EffectiveTraitsHash,
            BreedingEffort,
            SelfBreedingEffort,
            Gender
        );
    }
}
