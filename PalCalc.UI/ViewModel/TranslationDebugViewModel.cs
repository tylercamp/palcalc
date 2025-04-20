using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace PalCalc.UI.ViewModel
{
    public class TranslationCodeUsageDebugViewModel(LocalizationCodes code) : ObservableObject
    {
        public LocalizationCodes Code => code;

        private int count = Translator.WithCodeUsage(c => c[code]);
        public int Count
        {
            get => count;
            private set => SetProperty(ref count, value);
        }

        public void Refresh()
        {
            Count = Translator.WithCodeUsage(c => c[code]);
        }
    }

    public partial class TranslationLocaleDebugViewModel(TranslationLocale locale, List<ITranslationError> errors) : ObservableObject
    {
        public string TabTitle { get; } =
            $"{locale}" + (
                Translator.Localizations[locale].ContainsKey(LocalizationCodes.LC_ITL_LABEL)
                    ? " | " + Translator.Localizations[locale][LocalizationCodes.LC_ITL_LABEL]
                    : ""
            );

        public List<ITranslationError> Errors { get; } = errors.OrderBy(e => e.GetType().Name).ThenBy(e => e.Message).ToList();
    }

    public partial class TranslationDebugViewModel : ObservableObject
    {
        public static TranslationDebugViewModel DesignerInstance { get; } =
            new TranslationDebugViewModel([
                new MissingTranslationError(TranslationLocale.fr, LocalizationCodes.LC_ABOUT_APP, "Example expected"),
                new MissingArgumentError(TranslationLocale.fr, LocalizationCodes.LC_ADD_NEW_SAVE, ["Test"], "Example expected with {Test}"),
                new UnexpectedArgumentError(TranslationLocale.fr, LocalizationCodes.LC_ANY_PLAYER, ["Test"], "Example expected with {Test2}"),
                new UnexpectedTranslationError(TranslationLocale.fr, "FOO"),
            ]);

        List<ITranslationError> translationErrors;
        public TranslationDebugViewModel(List<ITranslationError> translationErrors)
        {
            this.translationErrors = translationErrors;

            LocaleErrors = translationErrors
                .GroupBy(e => e.Locale)
                .Select(g => new TranslationLocaleDebugViewModel(g.Key, g.ToList()))
                .ToList();

            ItlCodeReferenceCounts = Translator.CodeToArgs.Keys.Select(code => new TranslationCodeUsageDebugViewModel(code)).OrderBy(u => u.Code.ToString()).ToList();

            RefreshCountsCommand = new RelayCommand(() =>
            {
                foreach (var item in ItlCodeReferenceCounts)
                    item.Refresh();
            });
        }

        public List<TranslationLocaleDebugViewModel> LocaleErrors { get; }

        public List<TranslationCodeUsageDebugViewModel> ItlCodeReferenceCounts { get; }

        public IRelayCommand RefreshCountsCommand { get; }
    }
}
