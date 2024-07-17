using PalCalc.Model;
using QuickGraph;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace PalCalc.UI.Localization
{
    // note: code-gen of `LocalizationCodes` handled by ResXResourceManager extension, must be installed in order to
    // regenerate `LocalizationCodes` enum
    //
    // https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager

    public static partial class Translator
    {
        private static ILogger logger = Log.ForContext(typeof(Translator));

        // debug-only prop; when enabled, all text is replaced with placeholder text so we can easily see what isn't being localized
        public const bool DEBUG_DISABLE_TRANSLATIONS = false;

        // var-name -> '|'-separated list of named format args
        private static Dictionary<LocalizationCodes, string> codeToFormat;
        public static Dictionary<LocalizationCodes, string> CodeToArgs =>
            codeToFormat ??= ReadAllResources(LocalizationCodesResx.ResourceManager)
                .ToDictionary(kvp => Enum.Parse<LocalizationCodes>(kvp.Key), kvp => kvp.Value);


        private static Dictionary<TranslationLocale, Dictionary<LocalizationCodes, string>> localizations;
        public static Dictionary<TranslationLocale, Dictionary<LocalizationCodes, string>> Localizations
        {
            get
            {
                Init();
                return localizations;
            }

            private set => localizations = value;
        }


        private static Dictionary<LocalizationCodes, StoredLocalizableText> translations;
        public static Dictionary<LocalizationCodes, StoredLocalizableText> Translations
        {
            get
            {
                Init();
                return translations;
            }

            private set => translations = value;
        }

        public static event Action LocaleUpdated;

        private static TranslationLocale currentLocale = TranslationLocale.en;
        public static TranslationLocale CurrentLocale
        {
            get => currentLocale;
            set
            {
                currentLocale = value;

                foreach (var t in Translations.Values)
                    t.Locale = value;

                LocaleUpdated?.Invoke();
            }
        }

        private static ILocalizedText separator;
        public static ILocalizedText ListSeparator => separator ??= LocalizationCodes.LC_LIST_SEPARATOR.Bind();

        public static DerivedLocalizableText<IEnumerable<ILocalizedText>> Join { get; } =
            new DerivedLocalizableText<IEnumerable<ILocalizedText>>(
                (locale, parts) => string.Join(ListSeparator.Value, parts.Select(p => p.Value))
            );
    }
}
