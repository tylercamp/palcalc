﻿using PalCalc.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    public class WildPalReference : IPalReference, ISurgeryCachingPalReference
    {
        public WildPalReference(Pal pal, IEnumerable<PassiveSkill> guaranteedPassives, int numPassives)
        {
            Pal = pal;
            SelfBreedingEffort = GameConstants.TimeToCatch(pal) / GameConstants.PassivesWildAtMostN[numPassives];
            EffectivePassives = guaranteedPassives.Concat(Enumerable.Range(0, numPassives).Select(i => new RandomPassiveSkill())).ToList();
            Gender = PalGender.WILDCARD;
            CapturesRequiredForGender = 1;

            if (guaranteedPassives.Any(t => !pal.GuaranteedPassivesInternalIds.Contains(t.InternalName))) throw new InvalidOperationException();
            if (EffectivePassives.Count > GameConstants.MaxTotalPassives) throw new InvalidOperationException();

            EffectivePassivesHash = EffectivePassives.Select(p => p.InternalName).SetHash();
            IVs = new IV_Set() { HP = IV_Random.Instance, Attack =  IV_Random.Instance, Defense = IV_Random.Instance };
        }

        private WildPalReference(Pal pal)
        {
            Pal = pal;
        }

        public Pal Pal { get; private set; }

        public List<PassiveSkill> EffectivePassives { get; private set; }

        public IV_Set IVs { get; private set; }

        public PalGender Gender { get; private set; }

        public List<PassiveSkill> ActualPassives => EffectivePassives;

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

        public int NumTotalSurgerySteps => 0;

        public int NumTotalGenderReversers => 0;

        public int NumTotalEggs => 0;

        public int EffectivePassivesHash { get; }

        private IPalReference WithGuaranteedGenderImpl(PalDB db, PalGender gender)
        {
            return new WildPalReference(Pal)
            {
                SelfBreedingEffort = SelfBreedingEffort,
                Gender = gender,
                EffectivePassives = EffectivePassives,
                IVs = IVs,
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

        private ConcurrentDictionary<int, IPalReference> surgeryResultCache = null;
        public ConcurrentDictionary<int, IPalReference> SurgeryResultCache => surgeryResultCache ??= new();

        public override bool Equals(object obj)
        {
            var asWild = obj as WildPalReference;
            if (ReferenceEquals(asWild, null)) return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override string ToString() => $"Captured {Gender} {Pal} w/ up to {EffectivePassives.Count} random passive skills";

        public override int GetHashCode() => HashCode.Combine(nameof(WildPalReference), Pal, Gender, EffectivePassivesHash);
    }
}
