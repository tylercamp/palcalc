using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.model;

namespace PalCalc
{
    internal class ParseCsvProgram
    {
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
            ("Kitsun", "Astegon", "Shadowbeak")
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
        }

        static void Main(string[] args)
        {
            List<Trait> traits = File
                // google sheets "Palworld: Breeding Combinations and Calculator (v1.3-014)"; "SkillData" tab (hidden)
                .ReadAllLines("traits.csv")
                .Skip(1)
                .Select(l => l.Split(","))
                .Select(cols => new TraitCsvRow(cols))
                .Select(row => new Trait
                {
                    Name = row.Name,
                    InternalName = row.InternalName
                })
                .ToList();

            List<Pal> pals = File
                // google sheets "Palworld: Breeding Combinations and Calculator (v1.3-014)"; "PalData" tab (hidden)
                // (required fixing some names / "variant" flags)
                .ReadAllLines("fulldata.csv")
                .Skip(1)
                .Select(l => l.Split(','))
                .Select(cols => new PalCsvRow(cols))
                .Select(row => new Pal
                {
                    Id = new PalId
                    {
                        Id = row.Id,
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

                int childPower = (int)Math.Floor((parent1.BreedingPower + parent2.BreedingPower + 1) / 2.0f);
                return pals.OrderBy(p => Math.Abs(p.BreedingPower - childPower)).ThenBy(p => p.InternalIndex).First();
            }

            var db = new PalDB
            {
                Breeding = pals
                    .SelectMany(parent1 => pals.Select(parent2 => (parent1, parent2)))
                    .Select(pair => pair.parent1.GetHashCode() > pair.parent2.GetHashCode() ? (pair.parent1, pair.parent2) : (pair.parent2, pair.parent1))
                    .Distinct()
                    .Select(p => new BreedingResult
                    {
                        Parent1 = p.Item1,
                        Parent2 = p.Item2,
                        Child = Child(p.Item1, p.Item2)
                    })
                    .Select(r =>
                    {
                        var specialCase = SpecialCombos.Where(t => r.Parents.Any(p => p.Name == t.Item1) && r.Parents.Any(p => p.Name == t.Item2)).ToList();
                        if (specialCase.Count > 0)
                        {
                            r.Child = pals.Single(p => p.Name == specialCase.Single().Item3);
                        }
                        return r;
                    })
                    .ToList(),

                PalsById = pals.ToDictionary(p => p.Id),
                Traits = traits
            };

            File.WriteAllText("db.json", db.ToJson());
        }
    }
}