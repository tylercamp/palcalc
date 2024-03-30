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
    }

    interface IPalReference
    {
        Pal Pal { get; }
        List<Trait> Traits { get; }
        PalGender Gender { get; }

        IPalLocation Location { get; }

        IPalReference EnsureGender(PalGender gender);
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

        public IPalReference EnsureGender(PalGender gender)
        {
            if (gender != instance.Gender) throw new Exception($"Cannot make an owned {Gender} pal a {gender}");
            return this;
        }

        public override string ToString() => $"Owned {Gender} {Pal.Name} w/ ({string.Join(", ", Traits)}) in {Location}";
    }

    class WildcardPalReference : IPalReference
    {
        public WildcardPalReference(Pal pal)
        {
            Pal = pal;
        }

        public Pal Pal { get; private set; }

        public List<Trait> Traits { get; } = new List<Trait>();

        public PalGender Gender { get; private set; } = PalGender.WILDCARD;

        public IPalLocation Location { get; } = new CapturedPal();

        public IPalReference EnsureGender(PalGender gender)
        {
            return new WildcardPalReference(Pal) { Gender = gender };
        }

        public override string ToString() => $"Captured {Gender} {Pal}";
    }
}
