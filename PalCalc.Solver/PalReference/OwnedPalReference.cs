using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference
{
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
            if (gender != Gender) throw new Exception("Cannot force a gender change for owned pals");
            return this;
        }

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({EffectiveTraits.TraitsListToString()}) in {Location}";

        public override int GetHashCode() => HashCode.Combine(nameof(OwnedPalReference), UnderlyingInstance.GetHashCode());
    }
}
