using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    public class WildPalReference : IPalReference
    {
        public WildPalReference(Pal pal, FPassiveSet guaranteedPassives, int numRandomPassives)
        {
            Pal = pal;
            SelfBreedingEffort = GameConstants.TimeToCatch(pal) / GameConstants.PassivesWildAtMostN[numRandomPassives];
            EffectivePassives = guaranteedPassives.Concat(FPassiveSet.RepeatRandom(numRandomPassives));
            Gender = PalGender.WILDCARD;
            CapturesRequiredForGender = 1;

            if (guaranteedPassives.ModelObjects.Any(t => !pal.GuaranteedPassivesInternalIds.Contains(t.InternalName))) throw new InvalidOperationException();
            if (EffectivePassives.Count > GameConstants.MaxTotalPassives) throw new InvalidOperationException();

            IVs = new FIVSet(FIV.Random, FIV.Random, FIV.Random);
        }

        private WildPalReference(Pal pal)
        {
            Pal = pal;
        }

        public Pal Pal { get; private set; }

        public FPassiveSet EffectivePassives { get; private set; }

        public FIVSet IVs { get; private set; }

        public PalGender Gender { get; private set; }

        public FPassiveSet ActualPassives => EffectivePassives;

        public float TimeFactor => 1.0f;

        public IPalRefLocation Location { get; } = new CapturedRefLocation();

        public TimeSpan BreedingEffort => SelfBreedingEffort * CapturesRequiredForGender;

        // est. number of captured pals required to get a pal of the given gender (assuming you caught every
        // wild pal without checking for gender, not realistic but good enough)
        public int CapturesRequiredForGender { get; private set; }

        // used as the effort required to catch one
        public TimeSpan SelfBreedingEffort { get; private set; }

        public int TotalCost => 0;

        public int NumTotalBreedingSteps => 0;

        public int NumTotalEggs => 0;

        public int NumTotalWildPals => 1;

        private WildPalReference WithGuaranteedGenderImpl(PalDB db, PalGender gender, bool useReverser)
        {
            return new WildPalReference(Pal)
            {
                SelfBreedingEffort = SelfBreedingEffort,
                Gender = gender,
                EffectivePassives = EffectivePassives,
                IVs = IVs,
                CapturesRequiredForGender = useReverser ? 1 : gender switch
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

        private WildPalReference cachedMaleRef, cachedFemaleRef, cachedOppositeRef;
        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser)
        {
            if (Gender != PalGender.WILDCARD) throw new Exception("Wild pal has already been given a guaranteed gender");

            if (gender == PalGender.WILDCARD) return this;

            switch (gender)
            {
                case PalGender.WILDCARD: return this;
                case PalGender.OPPOSITE_WILDCARD: return cachedOppositeRef ??= WithGuaranteedGenderImpl(db, gender, useReverser);
                case PalGender.MALE: return cachedMaleRef ??= WithGuaranteedGenderImpl(db, gender, useReverser);
                case PalGender.FEMALE: return cachedFemaleRef ??= WithGuaranteedGenderImpl(db, gender, useReverser);

                default: throw new NotImplementedException();
            }
        }

        public override bool Equals(object obj)
        {
            var asWild = obj as WildPalReference;
            if (asWild is null) return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override string ToString() => $"Captured {Gender} {Pal} w/ up to {EffectivePassives.Count} random passive skills";

        public override int GetHashCode() => HashCode.Combine(nameof(WildPalReference), Pal, Gender, EffectivePassives);
    }
}
