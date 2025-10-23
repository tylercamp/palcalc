using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.UObject;
using Serilog;
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
        private static ILogger logger = Log.ForContext<MapReader>();

        public static MapInfo ReadMapInfo(IFileProvider provider)
        {
            var maps = provider.LoadPackageObject<UDataTable>(AssetPaths.MAP_PROPERTIES_PATH);

            MapInfo result = null;

            foreach (var row in maps.RowMap)
            {
                if (result != null)
                {
                    logger.Warning("Found map {Name}, but it was unexpected since map data was already collected", row.Key.Text);
                }

                var mapName = row.Key.Text;
                if (mapName != "MainMap")
                {
                    logger.Warning("Unrecognized map named {Name}, updates may be needed to support multiple maps, skipping", mapName);
                    continue;
                }

                var mapProperties = row.Value.Properties.ToDictionary(p => p.Name.Text, p => p.Tag);

                var mapMin = mapProperties["landScapeRealPositionMin"].GetValue<FVector>();
                var mapMax = mapProperties["landScapeRealPositionMax"].GetValue<FVector>();

                var textureProperties = mapProperties["textureDataMap"].GetValue<UScriptMap>();

                foreach (var texProp in textureProperties.Properties)
                {
                    var propName = texProp.Key.GetValue<FName>().Text;
                    if (propName != "FirstRegion")
                    {
                        logger.Warning("Unexpected map texture property {PropName}, skipping", propName);
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

            if (result == null)
            {
                logger.Warning("No map data was collected!");
            }

            return result;
        }
    }
}
