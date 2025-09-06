using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class BreedingResult
    {
        public GenderedPal Parent1, Parent2;
        public Pal Child;

        public IEnumerable<GenderedPal> Parents
        {
            get
            {
                yield return Parent1;
                yield return Parent2;
            }
        }

        public PalGender RequiredGenderOf(Pal parent)
        {
            if (parent == Parent1.Pal) return Parent1.Gender;
            else if (parent == Parent2.Pal) return Parent2.Gender;
            else throw new ArgumentException();
        }

        public GenderedPal OtherParent(Pal parent)
        {
            if (parent == Parent1.Pal) return Parent2;
            else if (parent == Parent2.Pal) return Parent1;
            else return null;
        }

        public GenderedPal OtherParent(GenderedPal parent)
        {
            if (parent == Parent1) return Parent2;
            else if (parent == Parent2) return Parent1;
            else return null;
        }

        public bool Matches(GenderedPal parent1, GenderedPal parent2) =>
            Matches(parent1.Pal, parent1.Gender, parent2.Pal, parent2.Gender);

        public bool Matches(Pal parent1, PalGender parent1Gender, Pal parent2, PalGender parent2Gender)
        {
            if (Parent1.Pal == parent1 && Parent2.Pal == parent2)
            {
                return (
                    (Parent1.Gender == PalGender.WILDCARD || Parent1.Gender == PalGender.OPPOSITE_WILDCARD || Parent1.Gender == parent1Gender) &&
                    (Parent2.Gender == PalGender.WILDCARD || Parent2.Gender == PalGender.OPPOSITE_WILDCARD || Parent2.Gender == parent2Gender)
                );
            }
            else if (Parent1.Pal == parent2 && Parent2.Pal == parent1)
            {
                return (
                    (Parent1.Gender == PalGender.WILDCARD || Parent1.Gender == PalGender.OPPOSITE_WILDCARD || Parent1.Gender == parent2Gender) &&
                    (Parent2.Gender == PalGender.WILDCARD || Parent2.Gender == PalGender.OPPOSITE_WILDCARD || Parent2.Gender == parent1Gender)
                );
            }
            else
            {
                return false;
            }
        }

        public override string ToString() => $"{Parent1} + {Parent2} = {Child}";

        public class Serialized
        {
            public PalId Parent1ID { get; set; }
            public PalGender Parent1Gender { get; set; }
            public PalId Parent2ID { get; set; }
            public PalGender Parent2Gender { get; set; }
            public PalId Child1ID { get; set; }
        }
    }
}
