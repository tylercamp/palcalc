﻿using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Objects.Core.i18N;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class LocalizationInfo(
        string languageCode,
        string palNameTextPath,
        string skillNameTextPath,
        string skillDescriptionTextPath,
        string commonTextPath,
        string humanNameTextPath
    )
    {
        private static Regex DoubleWhitespacePattern = new Regex(@"\s+");

        public string LanguageCode => languageCode;

        public Dictionary<string, string> ReadPalNames(IFileProvider provider)
        {
            var rawEntries = provider.LoadPackageObject<UDataTable>(palNameTextPath);
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
            var rawEntries = provider.LoadPackageObject<UDataTable>(skillNameTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap)
            {
                var skillId = entry.Key.Text.Replace("PASSIVE_", "");
                var content = entry.Value.Get<FText>("TextData");

                result.Add(skillId, DoubleWhitespacePattern.Replace(content.Text, " ").Trim());
            }

            return result.ToCaseInsensitive();
        }

        // note: returns description IDs
        public Dictionary<string, string> ReadSkillDescriptions(IFileProvider provider)
        {
            var rawEntries = provider.LoadPackageObject<UDataTable>(skillDescriptionTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap)
            {
                var skillId = entry.Key.Text;
                var content = entry.Value.Get<FText>("TextData");

                result.Add(skillId, content.Text.Trim());
            }

            return result;
        }

        public Dictionary<string, string> ReadCommonText(IFileProvider provider)
        {
            var rawEntries = provider.LoadPackageObject<UDataTable>(commonTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap)
            {
                var key = entry.Key.Text;
                var content = entry.Value.Get<FText>("TextData");

                result.Add(key, content.Text);
            }

            return result;
        }

        public Dictionary<string, string> ReadElementNames(IFileProvider provider)
        {
            return ReadCommonText(provider)
                .Where(kvp => kvp.Key.StartsWith("COMMON_ELEMENT_NAME_"))
                .MapKeys(k => k.Replace("COMMON_ELEMENT_NAME_", ""))
                .ToDictionary();
        }

        public Dictionary<string, string> ReadAttackNames(IFileProvider provider)
        {
            var rawEntries = provider.LoadPackageObject<UDataTable>(skillNameTextPath);
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var entry in rawEntries.RowMap)
            {
                var skillId = entry.Key.Text.Replace("ACTION_SKILL_", "");
                var content = entry.Value.Get<FText>("TextData");

                result.Add(skillId, DoubleWhitespacePattern.Replace(content.Text, " ").Trim());
            }

            return result.ToCaseInsensitive();
        }

        public Dictionary<string, string> ReadHumanNames(IFileProvider provider)
        {
            var rawEntries = provider.LoadPackageObject<UDataTable>(humanNameTextPath);
            return rawEntries.RowMap.ToDictionary(
                kvp => kvp.Key.Text,
                kvp => kvp.Value.Get<FText>("TextData").Text.Trim()
            );
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
                res.Add(new LocalizationInfo(
                    languageCode: lang,
                    palNameTextPath: $"{basePath}/DT_PalNameText_Common",
                    skillNameTextPath: $"{basePath}/DT_SkillNameText_Common",
                    skillDescriptionTextPath: $"{basePath}/DT_SkillDescText_Common",
                    commonTextPath: $"{basePath}/DT_UI_Common_Text_Common",
                    humanNameTextPath: $"{basePath}/DT_HumanNameText_Common"
                ));
            }

            res.Add(new LocalizationInfo(
                languageCode: "ja",
                palNameTextPath: "Pal/Content/Pal/DataTable/Text/DT_PalNameText",
                skillNameTextPath: "Pal/Content/Pal/DataTable/Text/DT_SkillNameText",
                skillDescriptionTextPath: "Pal/Content/Pal/DataTable/Text/DT_SkillDescText",
                commonTextPath: "Pal/Content/Pal/DataTable/Text/DT_UI_Common_Text",
                humanNameTextPath: "Pal/Content/Pal/DataTable/Text/DT_HumanNameText"
            ));
            return res;
        }
    }
}
