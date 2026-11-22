using PalCalc.GenDB.GameDataReaders;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.GenDB
{
    internal record UniqueBreedingCombo(
        (string Tribe, PalGender? Gender) Parent1,
        (string Tribe, PalGender? Gender) Parent2,
        string ChildId
    );

    internal class PalBreedingCalculator
    {
        private readonly Dictionary<string, Pal> palsById;
        private readonly Dictionary<string, UPal> characterRowsById;
        private readonly List<UniqueBreedingCombo> uniqueCombos;
        private readonly List<Pal> rankCandidates;

        public PalBreedingCalculator(List<Pal> pals, Dictionary<string, UPal> characterRowsById, List<UniqueBreedingCombo> uniqueCombos)
        {
            palsById = pals.ToDictionary(p => p.InternalName, StringComparer.OrdinalIgnoreCase);
            this.characterRowsById = characterRowsById;

            // Skip recipes the catalog can't represent. Parents match by Tribe
            var supportedTribes = pals.Select(p => characterRowsById[p.InternalName].Tribe).ToHashSet(StringComparer.OrdinalIgnoreCase);
            this.uniqueCombos = uniqueCombos
                .Where(c =>
                    supportedTribes.Contains(c.Parent1.Tribe) &&
                    supportedTribes.Contains(c.Parent2.Tribe) &&
                    palsById.ContainsKey(c.ChildId)
                )
                .ToList();

            // Find Pals which are only produced by the `CombiRank` / "Breeding Power" calculation, rather than recipes
            var recipeChildren = this.uniqueCombos.Select(c => c.ChildId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            rankCandidates = pals
                .Where(p => !characterRowsById[p.InternalName].IgnoreCombi && !recipeChildren.Contains(p.InternalName))
                .ToList();
        }

        public Pal Child(GenderedPal parent1, GenderedPal parent2)
        {
            // Child-pal calc determined by direct reverse engineering of v1.0.5.102999, see docres/research/palworld-breeding
            static bool Matches((string Tribe, PalGender? Gender) requirement, string tribe, PalGender gender) =>
                string.Equals(requirement.Tribe, tribe, StringComparison.OrdinalIgnoreCase) &&
                (requirement.Gender == null || requirement.Gender == gender);

            var parent1Row = characterRowsById[parent1.Pal.InternalName];
            var parent2Row = characterRowsById[parent2.Pal.InternalName];
            var recipe = uniqueCombos.FirstOrDefault(c =>
                (Matches(c.Parent1, parent1Row.Tribe, parent1.Gender) && Matches(c.Parent2, parent2Row.Tribe, parent2.Gender)) ||
                (Matches(c.Parent1, parent2Row.Tribe, parent2.Gender) && Matches(c.Parent2, parent1Row.Tribe, parent1.Gender))
            );

            if (recipe != null)
                return palsById[recipe.ChildId];

            var childPower = (parent1.Pal.BreedingPower + parent2.Pal.BreedingPower + 1) / 2;
            // BuildPals preserves table order, and MinBy keeps the first candidate when distance and priority tie.
            // The game falls back to Lamball (SheepBall) when its rank search finds no child.
            return rankCandidates.MinBy(p => (Math.Abs(p.BreedingPower - childPower), -(long)p.BreedingPowerPriority))
                ?? palsById["SheepBall"];
        }
    }
}
