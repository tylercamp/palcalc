using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    internal static class ParseCsv
    {
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
            public int Rarity => int.Parse(cols[11]);
            public int RunSpeed => int.Parse(cols[32]);
            public int RideSprintSpeed => int.Parse(cols[33]);
            public int Stamina => int.Parse(cols[51]);
            public int BreedPower => int.Parse(cols[53]);
            public int IndexOrder => int.Parse(cols[71]);
            public bool Mount => cols[84] == "TRUE";

            public string GuaranteedTrait1 => cols[67];
            public string GuaranteedTrait2 => cols[68];
            public string GuaranteedTrait3 => cols[69];
            public string GuaranteedTrait4 => cols[70];

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
            public string CodeName => cols[1];
            public bool IsPassiveTrait => cols[2] == "TRUE";
            public int Rank => int.Parse(cols[6]);
        }

        public static List<Trait> ReadTraits()
        {
            List<Trait> traits = File
                // google sheets "Palworld: Breeding Combinations and Calculator (v1.3-014)"; "SkillData" tab (hidden)
                .ReadAllLines("ref/traits.csv")
                .Skip(1)
                .Select(l => l.Split(","))
                .Select(cols => new TraitCsvRow(cols))
                .Where(row => row.IsPassiveTrait)
                .Select(row => new Trait(row.Name, row.CodeName, row.Rank))
                .ToList();

            return traits;
        }

        public static List<Pal> ReadPals()
        {
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
                    Stamina = row.Stamina,
                    Rarity = row.Rarity,
                    GuaranteedTraitInternalIds = new List<string>()
                    {
                        row.GuaranteedTrait1,
                        row.GuaranteedTrait2,
                        row.GuaranteedTrait3,
                        row.GuaranteedTrait4,
                    }.Where(t => t != "None").ToList()
                }).ToList();

            return pals;
        }
    }
}
