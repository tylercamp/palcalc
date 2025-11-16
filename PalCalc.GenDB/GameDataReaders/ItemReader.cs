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
    public class UItem
    {
        [FStructProperty]
        public string TypeA { get; set; }
        [FStructProperty]
        public string TypeB { get; set; }

        [FStructProperty]
        public string WazaID { get; set; }
    }

    internal class ItemReader
    {
        private static ILogger logger = Log.ForContext<ItemReader>();

        public static List<UItem> ReadItems(IFileProvider provider)
        {
            logger.Information("Reading items");

            var rawItems = provider.LoadPackageObject<UDataTable>(AssetPaths.ITEM_DATA_TABLE_PATH);

            var res = new List<UItem>();
            foreach (var row in rawItems.RowMap)
            {
                var item = row.Value.ToObject<UItem>();
                res.Add(item);
            }

            return res;
        }
    }
}
