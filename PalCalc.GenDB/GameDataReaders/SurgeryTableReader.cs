using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    public class USurgeryPassive
    {
        [FStructProperty]
        public string PassiveSkill { get; set; }

        [FStructProperty]
        public int Price { get; set; }

        [FStructProperty]
        public string RequireItemId { get; set; }
    }

    internal class SurgeryTableReader
    {
        private static ILogger logger = Log.ForContext<SurgeryTableReader>();

        public static List<USurgeryPassive> ReadSurgeryPassives(IFileProvider provider)
        {
            logger.Information("Reading surgery-table passives");

            var rawItems = provider.LoadPackageObject<UDataTable>(AssetPaths.OPERATING_TABLE_PASSIVES_PATH);

            var res = new List<USurgeryPassive>();
            foreach (var row in rawItems.RowMap)
            {
                var item = row.Value.ToObject<USurgeryPassive>();
                res.Add(item);
            }

            return res;
        }
    }
}
