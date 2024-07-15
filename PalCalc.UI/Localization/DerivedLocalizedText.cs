using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public class DerivedLocalizedText<Key>(Func<TranslationLocale, Key, string> converter, Key key) : ILocalizedText
    {
        public override string Value => Translator.DEBUG_DISABLE_TRANSLATIONS ? "DERIVED" : converter(Locale, key);
    }
}
