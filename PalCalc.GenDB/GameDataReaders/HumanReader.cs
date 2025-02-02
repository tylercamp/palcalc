using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class UHumanInfo
    {
        [FStructProperty]
        public string OverrideNameTextID { get; set; }

        public string InternalName { get; set; }
    }

    internal class HumanReader
    {
        public static List<UHumanInfo> ReadHumans(IFileProvider provider)
        {
            var rawHumans = provider.LoadObject<UDataTable>(AssetPaths.HUMANS_PATH);
            List<UHumanInfo> result = [];

            foreach (var row in rawHumans.RowMap)
            {
                var key = row.Key;
                var humanData = row.Value.ToObject<UHumanInfo>();

                humanData.InternalName = key.Text;
                result.Add(humanData);
            }

            return result;
        }
    }
}
