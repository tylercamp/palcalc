using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.UObject;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    internal class PalIconMappingsReader
    {

        private static ILogger logger = Log.ForContext<PalIconMappingsReader>();

        public static Dictionary<string, UTexture2D> ReadPalIconMappings(IFileProvider provider)
        {
            logger.Information("Reading pal icon mappings");
            var rawMappings = provider.LoadObject<UDataTable>(AssetPaths.PAL_ICONS_MAPPING_PATH);
            var res = new Dictionary<string, UTexture2D>();

            foreach (var entry in rawMappings.RowMap)
            {
                var iconPath = entry.Value.Get<FSoftObjectPath>("Icon");
                if (iconPath.TryLoad<UTexture2D>(out var tex))
                    res.Add(entry.Key.Text, tex);
                else
                    logger.Warning("Unable to load icon for {Name} at {Path}", entry.Key.Text, iconPath.AssetPathName);
            }

            return res;
        }
    }
}
