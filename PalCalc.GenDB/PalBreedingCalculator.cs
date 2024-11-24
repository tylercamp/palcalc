using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    internal class UniqueBreedingCombo
    {
        public Pal Parent1 { get; set; }
        public PalGender? Parent1Gender { get; set; }

        public Pal Parent2 { get; set; }
        public PalGender? Parent2Gender { get; set; }

        public Pal Child { get; set; }
    }

    internal class PalBreedingCalculator(List<Pal> pals, List<UniqueBreedingCombo> uniqueCombos)
    {
        public Pal Child(GenderedPal parent1, GenderedPal parent2)
        {
            if (parent1.Pal == parent2.Pal) return parent1.Pal;

            var specialCombo = uniqueCombos.Where(c =>
                (parent1.Pal == c.Parent1 && parent2.Pal == c.Parent2) ||
                (parent2.Pal == c.Parent1 && parent1.Pal == c.Parent2)
            );

            if (specialCombo.Any())
            {
                return pals.Single(p =>
                    p == specialCombo.Single(c =>
                    {
                        bool Matches(GenderedPal parent, Pal pal, PalGender? gender) =>
                            parent.Pal == pal && (gender == null || parent.Gender == gender);

                        return (
                            (Matches(parent1, c.Parent1, c.Parent1Gender) && Matches(parent2, c.Parent2, c.Parent2Gender)) ||
                            (Matches(parent2, c.Parent1, c.Parent1Gender) && Matches(parent1, c.Parent2, c.Parent2Gender))
                        );
                    }).Child
                );
            }

            int childPower = (int)Math.Floor((parent1.Pal.BreedingPower + parent2.Pal.BreedingPower + 1) / 2.0f);
            return pals
                .Where(p => !uniqueCombos.Any(c => p == c.Child)) // pals produced by a special combo can _only_ be produced by that combo
                .OrderBy(p => Math.Abs(p.BreedingPower - childPower))
                .ThenBy(p => p.InternalIndex)
                // if there are two pals with the same internal index, prefer the non-variant pal
                .ThenBy(p => p.Id.IsVariant ? 1 : 0)
                .First();
        }
    }
}
