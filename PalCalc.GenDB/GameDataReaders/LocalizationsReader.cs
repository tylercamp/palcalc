using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Objects.Core.i18N;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class LocalizationInfo(string languageCode, string palNameTextPath, string skillNameTextPath, string commonTextPath)
    {
        private static Regex DoubleWhitespacePattern = new Regex(@"\s+");

        public string LanguageCode => languageCode;

        public Dictionary<string, string> ReadPalNames(IFileProvider provider)
        {
            var rawEntries = provider.LoadObject<UDataTable>(palNameTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap)
            {
                var palId = entry.Key.Text.Replace("PAL_NAME_", "");
                var content = entry.Value.Get<FText>("TextData");

                result.Add(palId, DoubleWhitespacePattern.Replace(content.Text, " ").Trim());
            }

            return result.ToCaseInsensitive();
        }

        public Dictionary<string, string> ReadSkillNames(IFileProvider provider)
        {
            var rawEntries = provider.LoadObject<UDataTable>(skillNameTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap)
            {
                var skillId = entry.Key.Text.Replace("PASSIVE_", "");
                var content = entry.Value.Get<FText>("TextData");

                result.Add(skillId, DoubleWhitespacePattern.Replace(content.Text, " ").Trim());
            }

            return result.ToCaseInsensitive();
        }

        public Dictionary<string, string> ReadElementNames(IFileProvider provider)
        {
            var rawEntries = provider.LoadObject<UDataTable>(commonTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap.Where(e => e.Key.Text.StartsWith("COMMON_ELEMENT_NAME_")))
            {
                var element = entry.Key.Text.Replace("COMMON_ELEMENT_NAME_", "");
                var content = entry.Value.Get<FText>("TextData");

                result.Add(element, content.Text);
            }

            return result;
        }

        public Dictionary<string, string> ReadAttackNames(IFileProvider provider)
        {
            var rawEntries = provider.LoadObject<UDataTable>(skillNameTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap)
            {
                var skillId = entry.Key.Text.Replace("ACTION_SKILL_", "");
                var content = entry.Value.Get<FText>("TextData");

                result.Add(skillId, DoubleWhitespacePattern.Replace(content.Text, " ").Trim());
            }

            return result.ToCaseInsensitive();
        }
    }

    internal class LocalizationsReader
    {
        public static List<LocalizationInfo> FetchLocalizations(IFileProvider provider)
        {
            var l10nAssets = provider.Files.Select(f => f.Key).Where(p => p.StartsWith(AssetPaths.LOCALIZATIONS_BASE, StringComparison.InvariantCultureIgnoreCase)).ToList();

            var langs = l10nAssets.Select(p => p.Substring(AssetPaths.LOCALIZATIONS_BASE.Length + 1).Split('/').First()).Distinct().ToList();

            List<LocalizationInfo> res = [];
            foreach (var lang in langs)
            {
                var basePath = $"{AssetPaths.LOCALIZATIONS_BASE}/{lang}/Pal/DataTable/Text";
                res.Add(new LocalizationInfo(lang, $"{basePath}/DT_PalNameText", $"{basePath}/DT_SkillNameText", $"{basePath}/DT_UI_Common_Text"));
            }

            res.Add(new LocalizationInfo("ja", "Pal/Content/Pal/DataTable/Text/DT_PalNameText", "Pal/Content/Pal/DataTable/Text/DT_SkillNameText", "Pal/Content/Pal/DataTable/Text/DT_UI_Common_Text"));
            return res;
        }
    }
}
