using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
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
                                OnTranslationError?.Invoke(new UnexpectedTranslationError(l, kvp.Key));
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
                var locale = localeKvp.Key;
                foreach (var codeKvp in CodeToArgs)
                {
                    var code = codeKvp.Key;

                    var fallbackValue = fallbackLocalization.GetValueOrElse(code, "MISSING TRANSLATION: " + code);

                    if (!localeKvp.Value.ContainsKey(code))
                    {
                        logger.Warning($"Locale {locale} missing translation for {code}");
                        OnTranslationError?.Invoke(new MissingTranslationError(locale, code, fallbackValue));

                        localeKvp.Value.Add(codeKvp.Key, fallbackValue);
                    }
                    else
                    {
                        var paramRegex = new Regex(@"\{([^}]+)}");
                        var paramMatches = paramRegex
                            .Matches(localeKvp.Value[code])
                            .Select(m => m.Groups[1].Value)
                            .ToList();

                        var expectedParams = StoredLocalizableText.ParseParameters(codeKvp.Value);

                        var missingParams = expectedParams.Except(paramMatches).ToList();
                        var extraParams = paramMatches.Except(expectedParams).ToList();

                        if (missingParams.Any())
                        {
                            logger.Warning("Locale {locale} translation of {code} missing params {params}", locale, code, missingParams);
                            OnTranslationError?.Invoke(new MissingArgumentError(locale, code, missingParams, fallbackValue));
                        }

                        if (extraParams.Any())
                        {
                            logger.Warning("Locale {locale} translation of {code} as unexpected params {params}", locale, code, extraParams);
                            OnTranslationError?.Invoke(new UnexpectedArgumentError(locale, code, extraParams, fallbackValue));
                        }

                        if (missingParams.Any() || extraParams.Any())
                        {
                            localeKvp.Value[code] = fallbackValue;
                        }
                    }
                }
            }

            Localizations = result;

            Translations = CodeToArgs.Keys.ToDictionary(code => code, code => new StoredLocalizableText(code) { Locale = CurrentLocale });
        }
    }
}
