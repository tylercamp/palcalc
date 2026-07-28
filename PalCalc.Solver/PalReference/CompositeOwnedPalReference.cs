using PalCalc.Model;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    /// <summary>
    /// Represents owned male and female copies that can be used as one
    /// wildcard-gender candidate. A later breeding step selects whichever
    /// concrete copy has the required gender.
    /// 
    /// These pals _should_, but are not _guaranteed_, to have the same set of passives:
    /// 
    /// - If two pals have different desired passives, they should NOT be made composite.
    /// - Conversely, if one pal has a desired passive, both pals will have that desired passive.
    /// - The passives for this reference will match whichever pal has the most passives.
    /// </summary>
    public class CompositeOwnedPalReference : IPalReference
    {
        private static IV_Value PropagateIVs(IV_Value a, IV_Value b)
        {
            if (a != IV_Value.Random && b != IV_Value.Random)
            {
                return IV_Value.Merge(a, b);
            }
            else
            {
                return IV_Value.Random;
            }
        }

        public CompositeOwnedPalReference(OwnedPalReference male, OwnedPalReference female)
        {
            Male = male;
            Female = female;

            Location = new CompositeRefLocation(male.Location, female.Location);

            // effective passives based on which pal has the most irrelevant passives
            EffectivePassives = male.EffectivePassives.Count > female.EffectivePassives.Count ? male.EffectivePassives : female.EffectivePassives;
            EffectivePassivesHash = EffectivePassives.Select(p => p.InternalName).SetHash();

            ActualPassives = Male.ActualPassives.Intersect(Female.ActualPassives).ToList();
            while (ActualPassives.Count < EffectivePassives.Count) ActualPassives.Add(new RandomPassiveSkill());

            TimeFactor = ActualPassives.ToTimeFactor();

            IVs = new IV_Set()
            {
                HP = PropagateIVs(male.IVs.HP, female.IVs.HP),
                Attack = PropagateIVs(male.IVs.Attack, female.IVs.Attack),
                Defense = PropagateIVs(male.IVs.Defense, female.IVs.Defense)
            };
        }

        public OwnedPalReference Male { get; }
        public OwnedPalReference Female { get; }

        public Pal Pal => Male.Pal;

        public List<PassiveSkill> EffectivePassives { get; private set; }

        public int EffectivePassivesHash { get; private set; }

        public List<PassiveSkill> ActualPassives { get; }

        public IV_Set IVs { get; }

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public int NumTotalBreedingSteps { get; } = 0;

        public int NumTotalEggs { get; } = 0;

        public int NumTotalWildPals { get; } = 0;

        public IPalRefLocation Location { get; }

        public float TimeFactor { get; }

        public TimeSpan BreedingEffort { get; } = TimeSpan.Zero;

        public TimeSpan SelfBreedingEffort { get; } = TimeSpan.Zero;

        public int TotalCost => 0;

        public bool IsOutdated { get; set; }

        private CompositeOwnedPalReference oppositeWildcardReference;
        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser)
        {
            // Both concrete genders are already owned, so a reverser is not
            // needed.

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

        public override bool Equals(object obj)
        {
            var asComposite = obj as CompositeOwnedPalReference;
            if (ReferenceEquals(asComposite, null)) return false;

            return GetHashCode() == obj.GetHashCode();
        }

        // TODO - maybe just use Pal, PassivesHash, Gender, IVs? don't need hashes specific to the instances chosen?
        public override int GetHashCode() =>
            HashCode.Combine(
                nameof(CompositeOwnedPalReference),
                Male, Female,
                EffectivePassivesHash,
                Gender,
                IVs
            );
    }
}
