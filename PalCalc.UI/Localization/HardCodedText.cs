using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public class HardCodedText(string content) : ILocalizedText
    {
        public override string Value => Translator.DEBUG_DISABLE_TRANSLATIONS ? "HARD-CODED" : content;

        public static HardCodedText Empty { get; } = new HardCodedText("");
    }
}
