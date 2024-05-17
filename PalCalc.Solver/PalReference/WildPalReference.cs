using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    public class WildPalReference : IPalReference
    {
        public WildPalReference(Pal pal, IEnumerable<Trait> guaranteedTraits, int numTraits)
        {
            Pal = pal;
            SelfBreedingEffort = GameConstants.TimeToCatch(pal) / GameConstants.TraitWildAtMostN[numTraits];
            EffectiveTraits = guaranteedTraits.Concat(Enumerable.Range(0, numTraits).Select(i => new RandomTrait())).ToList();
            Gender = PalGender.WILDCARD;
            CapturesRequiredForGender = 1;

            if (guaranteedTraits.Any(t => !pal.GuaranteedTraitInternalIds.Contains(t.InternalName))) throw new InvalidOperationException();
            if (EffectiveTraits.Count > GameConstants.MaxTotalTraits) throw new InvalidOperationException();

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
        public int CapturesRequiredForGender { get; private set; }

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
                EffectiveTraits = EffectiveTraits,
                CapturesRequiredForGender = gender switch
                {
                    PalGender.WILDCARD => 1,
                    PalGender.OPPOSITE_WILDCARD =>
                        (int)Math.Round(
                            1 / Math.Min(
                                db.BreedingGenderProbability[Pal][PalGender.MALE],
                                db.BreedingGenderProbability[Pal][PalGender.FEMALE]
                            )
                        ),
                    PalGender.MALE => (int)Math.Round(1 / db.BreedingGenderProbability[Pal][PalGender.MALE]),
                    PalGender.FEMALE => (int)Math.Round(1 / db.BreedingGenderProbability[Pal][PalGender.FEMALE]),
                    _ => throw new NotImplementedException()
                }
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

        public override int GetHashCode() => HashCode.Combine(nameof(WildPalReference), Pal, Gender, EffectiveTraitsHash);
    }
}
