using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using System.Security.Cryptography;

namespace PalCalc.GenDB
{
    internal class BuildDBProgram
    {

        static void Main(string[] args)
        {
            var pals = new List<Pal>();
            pals.AddRange(ParseScrapedJson.ReadPals());

            var passives = new List<PassiveSkill>();
            passives.AddRange(ParseScrapedJson.ReadPassives());

            var localizations = ParseLocalizedNameJson.ParseLocalizedNames();

            foreach (var kvp in localizations)
            {
                var lang = kvp.Key;
                var i10n = kvp.Value;

                var missingPals = pals.Where(p => !i10n.PalsByLowerInternalName.ContainsKey(p.InternalName.ToLower())).ToList();
                var missingPassives = passives.Where(t => !i10n.TraitsByLowerInternalName.ContainsKey(t.InternalName.ToLower())).ToList();

                if (missingPals.Count > 0 || missingPassives.Count > 0)
                {
                    Console.WriteLine("{0} missing entries:", lang);

                    if (missingPals.Count > 0)
                    {
                        Console.WriteLine("Pals");
                        foreach (var p in missingPals) Console.WriteLine("- {0}", p.InternalName);
                    }

                    if (missingPassives.Count > 0)
                    {
                        Console.WriteLine("Passives");
                        foreach (var t in missingPassives) Console.WriteLine("- {0}", t.InternalName);
                    }
                }
            }

            foreach (var pal in pals)
                pal.LocalizedNames = localizations
                    .Select(kvp => (kvp.Key, kvp.Value.PalsByLowerInternalName.GetValueOrDefault(pal.InternalName.ToLower())))
                    .Where(p => p.Item2 != null)
                    .ToDictionary(p => p.Key, p => p.Item2);

            foreach (var skill in passives)
                skill.LocalizedNames = localizations
                    .Select(kvp => (kvp.Key, kvp.Value.TraitsByLowerInternalName.GetValueOrDefault(skill.InternalName.ToLower())))
                    .Where(p => p.Item2 != null)
                    .ToDictionary(p => p.Key, p => p.Item2);

            var specialCombos = ParseScrapedJson.ReadExclusiveBreedings();

            foreach (var (p1, p2, c) in specialCombos)
            {
                if (!pals.Any(p => p.InternalName == p1.Item1) || !pals.Any(p => p.InternalName == p2.Item1) || !pals.Any(p => p.InternalName == c))
                    throw new Exception("Unrecognized pal name");
            }

            foreach (var pal in pals)
                if (pal.GuaranteedPassivesInternalIds.Any(id => !passives.Any(t => t.InternalName == id)))
                    throw new Exception("Unrecognized passive skill ID");

            Pal Child(GenderedPal parent1, GenderedPal parent2)
            {
                if (parent1.Pal == parent2.Pal) return parent1.Pal;

                var specialCombo = specialCombos.Where(c =>
                    (parent1.Pal.InternalName == c.Item1.Item1 && parent2.Pal.InternalName == c.Item2.Item1) ||
                    (parent2.Pal.InternalName == c.Item1.Item1 && parent1.Pal.InternalName == c.Item2.Item1)
                );

                if (specialCombo.Any())
                {
                    return pals.Single(p =>
                        p.InternalName == specialCombo.Single(c =>
                        {
                            bool Matches(GenderedPal parent, string pal, PalGender? gender) =>
                                parent.Pal.InternalName == pal && (gender == null || parent.Gender == gender);

                            var ((p1, p1g), (p2, p2g), child) = c;

                            return (
                                (Matches(parent1, p1, p1g) && Matches(parent2, p2, p2g)) ||
                                (Matches(parent2, p1, p1g) && Matches(parent1, p2, p2g))
                            );
                        }).Item3
                    );
                }

                int childPower = (int)Math.Floor((parent1.Pal.BreedingPower + parent2.Pal.BreedingPower + 1) / 2.0f);
                return pals
                    .Where(p => !specialCombos.Any(c => p.InternalName == c.Item3)) // pals produced by a special combo can _only_ be produced by that combo
                    .OrderBy(p => Math.Abs(p.BreedingPower - childPower))
                    .ThenBy(p => p.InternalIndex)
                    // if there are two pals with the same internal index, prefer the non-variant pal
                    .ThenBy(p => p.Id.IsVariant ? 1 : 0)
                    .First();
            }

            var db = PalDB.MakeEmptyUnsafe("v13");
            db.Breeding = pals
                .SelectMany(parent1 => pals.Select(parent2 => (parent1, parent2)))
                .Select(pair => pair.parent1.GetHashCode() > pair.parent2.GetHashCode() ? (pair.parent1, pair.parent2) : (pair.parent2, pair.parent1))
                .Distinct()
                .SelectMany(pair => new[] {
                    (
                        new GenderedPal() { Pal = pair.Item1, Gender = PalGender.FEMALE },
                        new GenderedPal() { Pal = pair.Item2, Gender = PalGender.MALE }
                    ),
                    (
                        new GenderedPal() { Pal = pair.Item1, Gender = PalGender.MALE },
                        new GenderedPal() { Pal = pair.Item2, Gender = PalGender.FEMALE }
                    )
                })
                // get the results of breeding with swapped genders (for results where the child is determined by parent genders)
                .Select(p => new BreedingResult
                {
                    Parent1 = p.Item1,
                    Parent2 = p.Item2,
                    Child = Child(p.Item1, p.Item2)
                })
                // simplify cases where the child is the same regardless of gender
                .GroupBy(br => br.Child)
                .SelectMany(cg =>
                    cg
                        .GroupBy(br => (br.Parent1.Pal, br.Parent2.Pal))
                        .SelectMany(g =>
                        {
                            var results = g.ToList();
                            if (results.Count == 1) return results;

                            return
                            [
                                new BreedingResult()
                                {
                                    Parent1 = new GenderedPal()
                                    {
                                        Pal = results.First().Parent1.Pal,
                                        Gender = PalGender.WILDCARD
                                    },
                                    Parent2 = new GenderedPal()
                                    {
                                        Pal = results.First().Parent2.Pal,
                                        Gender = PalGender.WILDCARD
                                    },
                                    Child = results.First().Child
                                }
                            ];
                        })
                )
                .ToList();

            db.PalsById = pals.ToDictionary(p => p.Id);

            db.PassiveSkills = passives;

            var genderProbabilities = ParseScrapedJson.ReadGenderProbabilities();
            db.BreedingGenderProbability = pals.ToDictionary(
                p => p,
                p => genderProbabilities[p.InternalName]
            );

            db.MinBreedingSteps = BreedingDistanceMap.CalcMinDistances(db);

            File.WriteAllText("../PalCalc.Model/db.json", db.ToJson());
        }
    }
}