using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class MapInfo
    {
        public double MapMinX { get; set; }
        public double MapMinY { get; set; }
        public double MapMaxX { get; set; }
        public double MapMaxY { get; set; }

        public UTexture2D MapTexture { get; set; }
    }

    internal class MapReader
    {
        public static MapInfo ReadMapInfo(IFileProvider provider)
        {
            var maps = provider.LoadObject<UDataTable>(AssetPaths.MAP_PROPERTIES_PATH);

            MapInfo result = null;

            foreach (var row in maps.RowMap)
            {
                if (result != null)
                {
                    // TODO log
                }

                var mapName = row.Key.Text;
                if (mapName != "MainMap")
                {
                    // TODO log
                    continue;
                }

                var mapProperties = row.Value.Properties.ToDictionary(p => p.Name.Text, p => p.Tag);

                var mapMin = mapProperties["landScapeRealPositionMin"].GetValue<FVector>();
                var mapMax = mapProperties["landScapeRealPositionMax"].GetValue<FVector>();

                var textureProperties = mapProperties["textureDataMap"].GetValue<UScriptMap>();

                foreach (var texProp in textureProperties.Properties)
                {
                    if (texProp.Key.GetValue<FName>().Text != "FirstRegion")
                    {
                        // TODO log
                        continue;
                    }

                    var regionProps = texProp.Value.GetValue<FStructFallback>().Properties.ToDictionary(p => p.Name.Text, p => p.Tag);

                    var regionTexturePath = regionProps["Texture"].GetValue<FSoftObjectPath>();
                    regionTexturePath.TryLoad<UTexture2D>(out var regionTexture);

                    result = new MapInfo()
                    {
                        MapMinX = mapMin.X,
                        MapMinY = mapMin.Y,

                        MapMaxX = mapMax.X,
                        MapMaxY = mapMax.Y,

                        MapTexture = regionTexture
                    };
                }
            }

            // TODO log
            return result;
        }
    }
}
