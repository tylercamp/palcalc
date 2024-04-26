using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;

namespace PalCalc.GenDB
{
    internal class ParseCsvProgram
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

        static List<(String, String, String)> SpecialCombos = new List<(String, String, String)>()
        {
            ("Relaxaurus", "Sparkit", "Relaxaurus Lux"),
            ("Incineram", "Maraith", "Incineram Noct"),
            ("Mau", "Pengullet", "Mau Cryst"),
            ("Vanwyrm", "Foxcicle", "Vanwyrm Cryst"),
            ("Eikthyrdeer", "Hangyu", "Eikthyrdeer Terra"),
            ("Elphidran", "Surfent", "Elphidran Aqua"),
            ("Pyrin", "Katress", "Pyrin Noct"),
            ("Mammorest", "Wumpo", "Mammorest Cryst"),
            ("Mossanda", "Grizzbolt", "Mossanda Lux"),
            ("Dinossom", "Rayhound", "Dinossom Lux"),
            ("Jolthog", "Pengullet", "Jolthog Cryst"),
            ("Frostallion", "Helzephyr", "Frostallion Noct"),
            ("Kingpaca", "Reindrix", "Kingpaca Cryst"),
            ("Lyleen", "Menasting", "Lyleen Noct"),
            ("Leezpunk", "Flambelle", "Leezpunk Ignis"),
            ("Blazehowl", "Felbat", "Blazehowl Noct"),
            ("Robinquill", "Fuddler", "Robinquill Terra"),
            ("Broncherry", "Fuack", "Broncherry Aqua"),
            ("Surfent", "Dumud", "Surfent Terra"),
            ("Gobfin", "Rooby", "Gobfin Ignis"),
            ("Suzaku", "Jormuntide", "Suzaku Aqua"),
            ("Reptyro", "Foxcicle", "Reptyro Cryst"),
            ("Hangyu", "Swee", "Hangyu Cryst"),
            ("Mossanda", "Petallia", "Lyleen"),
            ("Vanwyrm", "Anubis", "Faleris"),
            ("Mossanda", "Rayhound", "Grizzbolt"),
            ("Grizzbolt", "Relaxaurus", "Orserk"),
            ("Kitsun", "Astegon", "Shadowbeak"),

            // these pals can only be produced by breeding two of the same parents of the same pal
            ("Frostallion", "Frostallion", "Frostallion"),
            ("Jetragon", "Jetragon", "Jetragon"),
            ("Paladius", "Paladius", "Paladius"),
            ("Necromus", "Necromus", "Necromus"),
            ("Jormuntide Ignis", "Jormuntide Ignis", "Jormuntide Ignis"),
        };


        // Special-case probabilities of breeding a given pal as a male
        static List<(String, float)> SpecialMaleProbabilities = new List<(string, float)>()
        {
            ("Kingpaca", 0.9f),
            ("Kingpaca Cryst", 0.9f),
            ("Warsect", 0.85f),
            ("Lovander", 0.3f),
            ("Lyleen", 0.3f),
            ("Lyleen Noct", 0.3f),
            ("Dazzi", 0.2f),
            ("Mozzarina", 0.2f),
            ("Elizabee", 0.1f),
            ("Beegarde", 0.1f),
        };
        

        class PalCsvRow
        {
            string[] cols;
            public PalCsvRow(string[] cols)
            {
                this.cols = cols;
            }

            public string Name => cols[0];
            public string CodeName => cols[1];
            public int Id => int.Parse(cols[8]);
            public bool IsVariant => cols[9].Trim() == "B";
            public int RunSpeed => int.Parse(cols[32]);
            public int RideSprintSpeed => int.Parse(cols[33]);
            public int Stamina => int.Parse(cols[51]);
            public int BreedPower => int.Parse(cols[53]);
            public int IndexOrder => int.Parse(cols[71]);
            public bool Mount => cols[84] == "TRUE";
            public MountType MountType
            {
                get
                {
                    switch (cols[85])
                    {
                        case "": return MountType.None;
                        case "Ground": return MountType.Ground;
                        case "Swim": return MountType.Swim;
                        case "Fly": return MountType.Fly;
                        case "Fly+Land": return MountType.FlyLand;
                        default: throw new Exception("unrecognized mount type: " + cols[85]);
                    }
                }
            }
        }

        class TraitCsvRow
        {
            string[] cols;
            public TraitCsvRow(string[] cols)
            {
                this.cols = cols;
            }

            public string Name => cols[0];
            public string InternalName => cols[1];
            public bool IsPassiveTrait => cols[2] == "TRUE";
            public int Rank => int.Parse(cols[6]);
        }

        static void Main(string[] args)
        {
            List<Trait> traits = File
                // google sheets "Palworld: Breeding Combinations and Calculator (v1.3-014)"; "SkillData" tab (hidden)
                .ReadAllLines("ref/traits.csv")
                .Skip(1)
                .Select(l => l.Split(","))
                .Select(cols => new TraitCsvRow(cols))
                .Where(row => row.IsPassiveTrait)
                .Select(row => new Trait(row.Name, row.InternalName, row.Rank))
                .ToList();

            List<Pal> pals = File
                // google sheets "Palworld: Breeding Combinations and Calculator (v1.3-014)"; "PalData" tab (hidden)
                // (required fixing some names / "variant" flags)
                .ReadAllLines("ref/fulldata.csv")
                .Skip(1)
                .Select(l => l.Split(','))
                .Select(cols => new PalCsvRow(cols))
                .Select(row => new Pal
                {
                    Id = new PalId
                    {
                        PalDexNo = row.Id,
                        IsVariant = row.IsVariant
                    },
                    Name = row.Name,
                    InternalName = row.CodeName,
                    InternalIndex = row.IndexOrder,
                    BreedingPower = row.BreedPower,
                    CanMount = row.Mount,
                    MountType = row.MountType,
                    RideSprintSpeed = row.RideSprintSpeed,
                    RideWalkSpeed = row.RunSpeed,
                    Stamina = row.Stamina
                }).ToList();

            Pal Child(Pal parent1, Pal parent2)
            {
                if (parent1 == parent2) return parent1;

                var specialCombo = SpecialCombos.Where(c =>
                    (parent1.Name == c.Item1 && parent2.Name == c.Item2) ||
                    (parent2.Name == c.Item1 && parent1.Name == c.Item2)
                );

                if (specialCombo.Any())
                {
                    return pals.Single(p => p.Name == specialCombo.Single().Item3);
                }

                int childPower = (int)Math.Floor((parent1.BreedingPower + parent2.BreedingPower + 1) / 2.0f);
                return pals
                    .Where(p => !SpecialCombos.Any(c => p.Name == c.Item3)) // pals produced by a special combo can _only_ be produced by that combo
                    .OrderBy(p => Math.Abs(p.BreedingPower - childPower))
                    .ThenBy(p => p.InternalIndex)
                    .First();
            }

            var db = PalDB.MakeEmptyUnsafe();
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

            db.BreedingGenderProbability = pals.ToDictionary(
                p => p,
                p =>
                {
                    if (SpecialMaleProbabilities.Any(s => s.Item1 == p.Name))
                    {
                        var maleProbability = SpecialMaleProbabilities.Single(s => s.Item1 == p.Name).Item2;
                        return new Dictionary<PalGender, float>()
                        {
                            { PalGender.MALE, maleProbability },
                            { PalGender.FEMALE, 1 - maleProbability },
                        };
                    }
                    else
                    {
                        return new Dictionary<PalGender, float>()
                        {
                            { PalGender.MALE, 0.5f },
                            { PalGender.FEMALE, 0.5f },
                        };
                    }
                }
            );

            db.MinBreedingSteps = CalcMinDistances(db);

            File.WriteAllText("../PalCalc.Model/db.json", db.ToJson());
        }
    }
}