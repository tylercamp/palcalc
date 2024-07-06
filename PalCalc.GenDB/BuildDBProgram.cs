using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using System.Security.Cryptography;

namespace PalCalc.GenDB
{
    internal class BuildDBProgram
    {
        // min. number of times you need to breed Key1 to get a Key2 (to prune out path checks between pals which would exceed the max breeding steps)
        static Dictionary<Pal, Dictionary<Pal, int>> CalcMinDistances(PalDB db)
        {
            Logging.InitCommonFull();

            Dictionary<Pal, Dictionary<Pal, int>> palDistances = new Dictionary<Pal, Dictionary<Pal, int>>();

            foreach (var p in db.Pals)
                palDistances.Add(p, new Dictionary<Pal, int>() { { p, 0 } });

            List<(Pal, Pal)> toCheck = new List<(Pal, Pal)>(db.Pals.SelectMany(p => db.Pals.Where(i => i != p).Select(p2 => (p, p2))));
            bool didUpdate = true;

            while (didUpdate)
            {
                didUpdate = false;

                List<(Pal, Pal)> resolved = new List<(Pal, Pal)>();
                List<(Pal, Pal)> unresolved = new List<(Pal, Pal)>();
                foreach (var next in toCheck)
                {
                    var src = next.Item1;
                    var target = next.Item2;

                    // check if there's a direct way to breed from src to target
                    if (db.BreedingByChild[target].ContainsKey(src))
                    {
                        if (!palDistances[src].ContainsKey(target) || palDistances[src][target] != 1)
                        {
                            didUpdate = true;
                            palDistances[src][target] = 1;
                            resolved.Add(next);
                        }
                        continue;
                    }

                    // check if there's a possible child of this `src` with known distance to target
                    var childWithShortestDistance = db.BreedingByParent[src].Values.Select(b => b.Child).Where(child => palDistances[child].ContainsKey(target)).OrderBy(child => palDistances[child][target]).FirstOrDefault();
                    if (childWithShortestDistance != null)
                    {
                        if (!palDistances[src].ContainsKey(target) || palDistances[src][target] != palDistances[childWithShortestDistance][target] + 1)
                        {
                            didUpdate = true;
                            palDistances[src][target] = palDistances[childWithShortestDistance][target] + 1;
                            resolved.Add(next);
                        }
                        continue;
                    }

                    unresolved.Add(next);
                }

                Console.WriteLine("Resolved {0} entries with {1} left unresolved", resolved.Count, unresolved.Count);

                if (!didUpdate)
                {
                    // the remaining (src,target) pairs are impossible
                    foreach (var p in unresolved)
                    {
                        palDistances[p.Item1].Add(p.Item2, 10000);
                    }
                }
            }

            return palDistances;
        }

        static void Main(string[] args)
        {
            var pals = new List<Pal>();
            //pals.AddRange(ParseCsv.ReadPals());
            //pals.AddRange(ParseExtraJson.ReadPals());
            pals.AddRange(ParseScrapedJson.ReadPals());

            var traits = new List<Trait>();
            //traits.AddRange(ParseCsv.ReadTraits());
            //traits.AddRange(ParseExtraJson.ReadTraits());
            traits.AddRange(ParseScrapedJson.ReadTraits());

            var specialCombos = ParseScrapedJson.ReadExclusiveBreedings();

            foreach (var (p1, p2, c) in specialCombos)
            {
                if (!pals.Any(p => p.InternalName == p1) || !pals.Any(p => p.InternalName == p2) || !pals.Any(p => p.InternalName == c))
                    throw new Exception("Unrecognized pal name");
            }

            foreach (var pal in pals)
                if (pal.GuaranteedTraitInternalIds.Any(id => !traits.Any(t => t.InternalName == id)))
                    throw new Exception("Unrecognized trait ID");

            Pal Child(Pal parent1, Pal parent2)
            {
                if (parent1 == parent2) return parent1;

                var specialCombo = specialCombos.Where(c =>
                    (parent1.InternalName == c.Item1 && parent2.InternalName == c.Item2) ||
                    (parent2.InternalName == c.Item1 && parent1.InternalName == c.Item2)
                );

                if (specialCombo.Any())
                {
                    // TODO - Katress/Wixen breed result depends on which one is male/female, special combos
                    //        atm don't take gender into account
                    return pals.Single(p => p.InternalName == specialCombo.First().Item3);
                }

                int childPower = (int)Math.Floor((parent1.BreedingPower + parent2.BreedingPower + 1) / 2.0f);
                return pals
                    .Where(p => !specialCombos.Any(c => p.InternalName == c.Item3)) // pals produced by a special combo can _only_ be produced by that combo
                    .OrderBy(p => Math.Abs(p.BreedingPower - childPower))
                    .ThenBy(p => p.InternalIndex)
                    // if there are two pals with the same internal index, prefer the non-variant pal
                    .ThenBy(p => p.Id.IsVariant ? 1 : 0)
                    .First();
            }

            var db = PalDB.MakeEmptyUnsafe("v10");
            db.Breeding = pals
                .SelectMany(parent1 => pals.Select(parent2 => (parent1, parent2)))
                .Select(pair => pair.parent1.GetHashCode() > pair.parent2.GetHashCode() ? (pair.parent1, pair.parent2) : (pair.parent2, pair.parent1))
                .Distinct()
                .Select(p => new BreedingResult
                {
                    Parent1 = p.Item1,
                    Parent2 = p.Item2,
                    Child = Child(p.Item1, p.Item2)
                })
                .ToList();

            db.PalsById = pals.ToDictionary(p => p.Id);

            db.Traits = traits;

            var genderProbabilities = ParseScrapedJson.ReadGenderProbabilities();
            db.BreedingGenderProbability = pals.ToDictionary(
                p => p,
                p => genderProbabilities[p.InternalName]
            );

            db.MinBreedingSteps = CalcMinDistances(db);

            File.WriteAllText("../PalCalc.Model/db.json", db.ToJson());
        }
    }
}