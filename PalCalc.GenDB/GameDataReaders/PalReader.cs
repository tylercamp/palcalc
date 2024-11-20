using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using System;
using System.Collections.Generic;
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
        public int Price { get; set; }

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

        [FStructProperty]
        public bool IsBoss { get; set; }

        [FStructProperty]
        public bool IsTowerBoss { get; set; }

        [FStructProperty]
        public bool IsRaidBoss { get; set; }

        [FStructProperty]
        public string OverrideNameTextId { get; set; }

        public string AlternativeInternalName => OverrideNameTextId.Replace("PAL_NAME_", "", StringComparison.InvariantCultureIgnoreCase);

        // (assigned manually)
        public string InternalName { get; set; }

        public int InternalIndex { get; set; }
    }

    internal class PalReader
    {
        public static List<UPal> ReadPals(IFileProvider provider)
        {
            Console.WriteLine("Reading pals");
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
                    !(palData.IsBoss || palData.IsRaidBoss || palData.IsTowerBoss)
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
