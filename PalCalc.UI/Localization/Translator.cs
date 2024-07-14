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
    /*
     * Translation Code: Readable label, used as variable names in `LocalizationCodes`
     * Translation ID: Full ID of the translation code, contains parameters
     */

    public static partial class Translator
    {
        private static ILogger logger = Log.ForContext(typeof(Translator));


        // var-name -> ID
        private static Dictionary<string, string> codeToId;
        public static Dictionary<string, string> CodeToId => codeToId ??= ReadAllResources(LocalizationCodes.ResourceManager);

        private static Dictionary<TranslationLocale, Dictionary<string, string>> localizations;
        public static Dictionary<TranslationLocale, Dictionary<string, string>> Localizations
        {
            get
            {
                Init();
                return localizations;
            }

            private set => localizations = value;
        }


        private static Dictionary<string, LocalizableText> translations;
        public static Dictionary<string, LocalizableText> Translations
        {
            get
            {
                Init();
                return translations;
            }

            private set => translations = value;
        }

        public static event Action LocaleUpdated;

        // TODO - load from save
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


    }
}
