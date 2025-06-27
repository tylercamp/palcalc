using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        [FStructProperty]
        public bool IsTowerBoss { get; set; }

        [FStructProperty]
        public bool IsRaidBoss { get; set; }

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

        public int InternalIndex { get; set; }
    }

    internal class PalReader
    {
        private static ILogger logger = Log.ForContext<PalReader>();

        public static List<UPal> ReadPals(IFileProvider provider)
        {
            logger.Information("Reading pals");
            var rawPals = provider.LoadObject<UDataTable>(AssetPaths.PALS_PATH);
            List<UPal> result = [];

            // note: index order won't match other pal DBs exactly since we're not skipping Warsect Terra
            //       and we're giving "Gumoss (Special)" its own unique index value (rather than having it share
            //       with normal gumoss)
            //
            //       ordering will be the same, but values may be slightly offset
            int indexOrder = 1;
            foreach (var row in rawPals.RowMap)
            {
                var key = row.Key;
                var palData = row.Value.ToObject<UPal>();

                if (
                    palData.IsPal &&
                    palData.PalDexNum >= 0 &&
                    !(palData.IsBoss || palData.IsRaidBoss || palData.IsTowerBoss) &&
                    !key.Text.StartsWith("Quest_")
                )
                {
                    palData.InternalName = key.Text;
                    palData.InternalIndex = indexOrder++;
                    result.Add(palData);
                }
            }

            return result;
        }
    }
}
