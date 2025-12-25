using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
    /// <summary>
    /// Represents a pair of male and female instances of the same pal. This allows us to represent
    /// Male+Female owned pals to act as "wildcard" genders. (Without this the solver will tend to prefer
    /// redundantly breeding a pal of "opposite gender" compared to another pal step which has lots of
    /// requirements + breeding attempts. It wouldn't directly pair it with a male or female pal, since
    /// that would require breeding the "difficult" pal to have a specific gender.)
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

        private CompositeOwnedPalReference oppositeWildcardReference;
        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser)
        {
            // (we have direct reprs for both genders, no need to check useReverser)

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
