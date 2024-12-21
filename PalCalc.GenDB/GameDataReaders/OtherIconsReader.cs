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
                    provider.TryLoadObject<UTexture2D>(fullName, out var tex);
                    return tex;
                }
            );
        }

        public static OtherIcons ReadIcons(IFileProvider provider)
        {
            var result = new OtherIcons();

            result.ElementIcons = ReadIconsLike(provider, AssetPaths.ELEMENT_ICONS_BASE);
            result.SkillElementIcons = ReadIconsLike(provider, AssetPaths.SKILL_ELEMENT_ICONS_BASE);
            result.SkillRankIcons = ReadIconsLike(provider, AssetPaths.SKILL_RANK_ICONS_BASE);

            return result;
        }
    }
}
