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
                    return ReadAllResources(rm)
                        .Select<KeyValuePair<string, string>, (LocalizationCodes?, string)>(kvp =>
                        {
                            if (Enum.TryParse(kvp.Key, out LocalizationCodes code))
                                return (code, kvp.Value);
                            else
                            {
                                logger.Warning("Locale {locale} has unexpected TL ID: {id}", l, kvp.Key);
                                return (null, kvp.Value);
                            }
                        })
                        .Where(p => p.Item1 != null)
                        .ToDictionary(
                            kvp => kvp.Item1.Value,
                            kvp => kvp.Item2
                        );
                }
            );

            var fallbackLocalization = result[FallbackLocale];

            foreach (var localeKvp in result)
            {
                foreach (var codeKvp in CodeToFormat)
                {
                    if (!localeKvp.Value.ContainsKey(codeKvp.Key))
                    {
                        logger.Warning($"Locale {localeKvp.Key} missing translation for {codeKvp.Key}");

                        if (localeKvp.Key != FallbackLocale && fallbackLocalization.ContainsKey(codeKvp.Key))
                            localeKvp.Value.Add(codeKvp.Key, fallbackLocalization[codeKvp.Key]);
                        else
                            localeKvp.Value.Add(codeKvp.Key, "MISSING TRANSLATION: " + codeKvp.Key);
                    }
                }
            }

            Localizations = result;

            Translations = CodeToFormat.Keys.ToDictionary(code => code, code => new StoredLocalizableText(code) { Locale = CurrentLocale });
        }
    }
}
