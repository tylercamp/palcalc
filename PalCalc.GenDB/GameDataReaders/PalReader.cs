using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.GenDB.GameDataReaders
{
    class UPal
    {
        [FStructProperty]
        public bool IsPal { get; set; }

        [FStructProperty("ZukanIndex")]
        public int PalDexNum { get; set; }

        [FStructProperty("ZukanIndexSuffix")]
        public string PalDexNumSuffix { get; set; }

        [FStructProperty]
        public int Rarity { get; set; }

        [FStructProperty]
        public float Price { get; set; }

        [FStructProperty("CombiRank")]
        public int BreedingPower { get; set; }

        [FStructProperty("CombiDuplicatePriority")]
        public int BreedingPowerPriority { get; set; }

        [FStructProperty]
        public string Tribe { get; set; }

        public string TribeName => Tribe.Replace("EPalTribeID::", "");

        [FStructProperty]
        public int MaleProbability { get; set; }

        [FStructProperty]
        public string PassiveSkill1 { get; set; }

        [FStructProperty]
        public string PassiveSkill2 { get; set; }

        [FStructProperty]
        public string PassiveSkill3 { get; set; }

        [FStructProperty]
        public string PassiveSkill4 { get; set; }

        public List<string> GuaranteedPassives => new List<string>()
        {
            PassiveSkill1,
            PassiveSkill2,
            PassiveSkill3,
            PassiveSkill4
        }.Where(p => p != null && p.Length > 0 && p != "None").ToList();

        [FStructProperty]
        public bool IsBoss { get; set; }

        // The game skips this Pal when choosing a child by breeding rank. Recipes can still select it.
        [FStructProperty]
        public bool IgnoreCombi { get; set; }

        [FStructProperty]
        public bool IsTowerBoss { get; set; }

        [FStructProperty]
        public bool IsRaidBoss { get; set; }

        [FStructProperty]
        public bool Predator { get; set; }

        [FStructProperty]
        public string Size { get; set; }

        [FStructProperty]
        public int CraftSpeed { get; set; }

        [FStructProperty]
        public int Hp { get; set; }
        [FStructProperty]
        public int Defense { get; set; }
        [FStructProperty]
        public int Support { get; set; }
        [FStructProperty]
        public int ShotAttack { get; set; }
        [FStructProperty]
        public int WalkSpeed { get; set; }
        [FStructProperty]
        public int RunSpeed { get; set; }
        [FStructProperty]
        public int RideSprintSpeed { get; set; }
        [FStructProperty]
        public int TransportSpeed { get; set; }
        [FStructProperty]
        public int MaxFullStomach { get; set; }
        [FStructProperty]
        public int FoodAmount { get; set; }

        [FStructProperty]
        public bool Nocturnal { get; set; }

        [FStructProperty]
        public int Stamina { get; set; }

        [FStructProperty]
        public int WorkSuitability_EmitFlame { get; set; }
        [FStructProperty]
        public int WorkSuitability_Watering { get; set; }
        [FStructProperty]
        public int WorkSuitability_Seeding { get; set; }
        [FStructProperty]
        public int WorkSuitability_GenerateElectricity { get; set; }
        [FStructProperty]
        public int WorkSuitability_Handcraft { get; set; }
        [FStructProperty]
        public int WorkSuitability_Collection { get; set; }
        [FStructProperty]
        public int WorkSuitability_Deforest { get; set; }
        [FStructProperty]
        public int WorkSuitability_Mining { get; set; }
        [FStructProperty]
        public int WorkSuitability_OilExtraction { get; set; }
        [FStructProperty]
        public int WorkSuitability_ProductMedicine { get; set; }
        [FStructProperty]
        public int WorkSuitability_Cool { get; set; }
        [FStructProperty]
        public int WorkSuitability_Transport { get; set; }
        [FStructProperty]
        public int WorkSuitability_MonsterFarm { get; set; }

        [FStructProperty]
        public string OverrideNameTextId { get; set; }

        public string AlternativeInternalName => OverrideNameTextId.Replace("PAL_NAME_", "", StringComparison.InvariantCultureIgnoreCase);

        // (assigned manually)
        public string InternalName { get; set; }
    }

    internal class PalReader
    {
        private static ILogger logger = Log.ForContext<PalReader>();

        public static List<UPal> ReadPals(IFileProvider provider)
        {
            logger.Information("Reading pals");
            var table = provider.LoadPackageObject<UDataTable>(AssetPaths.PALS_PATH);
            return table.RowMap.Select(row =>
            {
                var pal = row.Value.ToObject<UPal>();
                pal.InternalName = row.Key.Text;
                return pal;
            }).ToList();
        }
    }
}
