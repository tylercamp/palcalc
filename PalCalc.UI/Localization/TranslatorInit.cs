using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.Localization
{
    public static partial class Translator
    {
        private static readonly TranslationLocale FallbackLocale = TranslationLocale.en;
        private static object initLock = new();
        private static bool didInit = false;

        private static Dictionary<string, string> ReadAllResources(ResourceManager rm)
        {
            Dictionary<string, string> result = [];
            var codeEnum = rm.GetResourceSet(CultureInfo.InvariantCulture, true, true).GetEnumerator();
            while (codeEnum.MoveNext())
                result.Add(codeEnum.Key.ToString(), codeEnum.Value.ToString());

            return result;
        }

        public static void Init()
        {
            if (didInit) return;

            lock (initLock)
            {
                if (didInit) return;

                DoInit();
                didInit = true;
            }
        }

        private static void DoInit()
        {
            var result = Enum.GetValues<TranslationLocale>().ToDictionary(
                l => l,
                l =>
                {
                    var resxName = l.ToFormalName();
                    var rm = new ResourceManager("PalCalc.UI.Localization.Localizations." + resxName, typeof(Translator).Assembly);
                    return ReadAllResources(rm).ToDictionary(
                        kvp => CodeToId[kvp.Key],
                        kvp => kvp.Value
                    );
                }
            );

            var fallbackLocalization = result[FallbackLocale];

            foreach (var localeKvp in result)
            {
                foreach (var codeKvp in CodeToId)
                {
                    if (!localeKvp.Value.ContainsKey(codeKvp.Value))
                    {
                        logger.Warning($"Locale {localeKvp.Key} missing translation for {codeKvp.Key}");

                        if (localeKvp.Key != FallbackLocale && fallbackLocalization.ContainsKey(codeKvp.Value))
                            localeKvp.Value.Add(codeKvp.Value, fallbackLocalization[codeKvp.Value]);
                        else
                            localeKvp.Value.Add(codeKvp.Value, "MISSING TRANSLATION: " + codeKvp.Key);
                    }
                }

                var unexpectedCodes = localeKvp.Value.Keys.Where(id => !CodeToId.ContainsValue(id)).ToList();
                if (unexpectedCodes.Count > 0)
                {
                    logger.Warning("Locale {locale} has unexpected TL IDs: {ids}", localeKvp.Key, unexpectedCodes);
                }
            }

            Localizations = result;

            Translations = CodeToId.Values.ToDictionary(v => v, v => new StoredLocalizableText(v) { Locale = CurrentLocale });
        }
    }
}
