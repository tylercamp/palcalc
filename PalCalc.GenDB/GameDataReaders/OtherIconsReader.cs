using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    public class OtherIcons
    {
        public Dictionary<string, UTexture2D> ElementIcons { get; set; }

        public Dictionary<string, UTexture2D> SkillElementIcons { get; set; }

        public Dictionary<string, UTexture2D> SkillRankIcons { get; set; }

        public Dictionary<string, UTexture2D> WorkSuitabilityIcons { get; set; }

        public Dictionary<string, UTexture2D> StatusIcons { get; set; }

        public UTexture2D FoodIconOff { get; set; }
        public UTexture2D FoodIconOn { get; set; }


        public UTexture2D TimerIcon { get; set; }
        public UTexture2D DayIcon { get; set; }
        public UTexture2D NightIcon { get; set; }

        public UTexture2D DungeonIconSmall { get; set; }

        public UTexture2D SurgeryTableIcon { get; set; }
    }

    internal class OtherIconsReader
    {
        private static ILogger logger = Log.ForContext<OtherIconsReader>();

        private static Dictionary<string, UTexture2D> ReadIconsLike(IFileProvider provider, string basePath)
        {
            var files = provider.Files
                .Where(f => f.Key.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                .Where(f => f.Key.EndsWith("uasset", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return files.ToDictionary(
                f => f.Key.SubstringAfterLast('/'),
                f =>
                {
                    var fullName = f.Key.SubstringBeforeLast('.') + '.' + f.Value.NameWithoutExtension;
                    provider.TryLoadPackageObject<UTexture2D>(fullName, out var tex);
                    return tex;
                },
                StringComparer.InvariantCultureIgnoreCase
            );
        }

        public static OtherIcons ReadIcons(IFileProvider provider)
        {
            logger.Information("Reading other/misc. icon data");
            var result = new OtherIcons();

            result.ElementIcons = ReadIconsLike(provider, AssetPaths.ELEMENT_ICONS_BASE);
            result.SkillElementIcons = ReadIconsLike(provider, AssetPaths.SKILL_ELEMENT_ICONS_BASE);
            result.SkillRankIcons = ReadIconsLike(provider, AssetPaths.SKILL_RANK_ICONS_BASE);
            result.WorkSuitabilityIcons = ReadIconsLike(provider, AssetPaths.WORK_SUITABILITY_ICONS_BASE);
            result.StatusIcons = ReadIconsLike(provider, AssetPaths.STATUS_ICONS_BASE);

            result.FoodIconOn = provider.LoadPackageObject<UTexture2D>(AssetPaths.FOOD_ICON_ON_PATH);
            result.FoodIconOff = provider.LoadPackageObject<UTexture2D>(AssetPaths.FOOD_ICON_OFF_PATH);
            result.TimerIcon = provider.LoadPackageObject<UTexture2D>(AssetPaths.TIMER_ICON_PATH);
            result.DayIcon = provider.LoadPackageObject<UTexture2D>(AssetPaths.DAY_ICON_PATH);
            result.NightIcon = provider.LoadPackageObject<UTexture2D>(AssetPaths.NIGHT_ICON_PATH);

            result.DungeonIconSmall = provider.LoadPackageObject<UTexture2D>(AssetPaths.DUNGEON_ICON_SMALL_PATH);

            result.SurgeryTableIcon = provider.LoadPackageObject<UTexture2D>(AssetPaths.SURGERY_TABLE_ICON_PATH);

            return result;
        }
    }
}
