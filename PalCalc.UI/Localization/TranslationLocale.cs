using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public enum TranslationLocale
    {
        de, en, es, fr, it, ko, pt_BR, ru, zh_Hans, zh_Hant
    }

    public static class TranslationLocaleExtensions
    {
        // convert enum name to stored name for pals + localization files
        public static string ToFormalName(this TranslationLocale locale) => locale.ToString().Replace('_', '-');
    }
}
