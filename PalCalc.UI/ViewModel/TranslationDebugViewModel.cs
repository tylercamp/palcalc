using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    public class TranslationLocaleDebugViewModel(TranslationLocale locale, List<ITranslationError> errors)
    {
        public string TabTitle { get; } =
            $"{locale}" + (
                Translator.Localizations[locale].ContainsKey(LocalizationCodes.LC_ITL_LABEL)
                    ? " | " + Translator.Localizations[locale][LocalizationCodes.LC_ITL_LABEL]
                    : ""
            );

        public List<ITranslationError> Errors { get; } = errors.OrderBy(e => e.GetType().Name).ThenBy(e => e.Message).ToList();
    }

    public class TranslationDebugViewModel(List<ITranslationError> translationErrors)
    {
        public static TranslationDebugViewModel DesignerInstance { get; } =
            new TranslationDebugViewModel([
                new MissingTranslationError(TranslationLocale.fr, LocalizationCodes.LC_ABOUT_APP, "Example expected"),
                new MissingArgumentError(TranslationLocale.fr, LocalizationCodes.LC_ADD_NEW_SAVE, ["Test"], "Example expected with {Test}"),
                new UnexpectedArgumentError(TranslationLocale.fr, LocalizationCodes.LC_ANY_PLAYER, ["Test"], "Example expected with {Test2}"),
                new UnexpectedTranslationError(TranslationLocale.fr, "FOO"),
            ]);

        public List<TranslationLocaleDebugViewModel> LocaleErrors { get; } =
            translationErrors
                .GroupBy(e => e.Locale)
                .Select(g => new TranslationLocaleDebugViewModel(g.Key, g.ToList()))
                .ToList();
    }
}
