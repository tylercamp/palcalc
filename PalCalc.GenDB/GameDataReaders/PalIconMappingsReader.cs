using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    internal class PalIconMappingsReader
    {
        public static Dictionary<string, UTexture2D> ReadPalIconMappings(IFileProvider provider)
        {
            Console.WriteLine("Reading pal icon mappings");
            var rawMappings = provider.LoadObject<UDataTable>(AssetPaths.PAL_ICONS_MAPPING_PATH);
            var res = new Dictionary<string, UTexture2D>();

            foreach (var entry in rawMappings.RowMap)
            {
                var iconPath = entry.Value.Get<FSoftObjectPath>("Icon");
                if (iconPath.TryLoad<UTexture2D>(out var tex))
                    res.Add(entry.Key.Text, tex);
                else
                    Console.WriteLine("Unable to load icon for " + entry.Key.Text + " at " + iconPath.AssetPathName);
            }

            return res;
        }
    }
}
