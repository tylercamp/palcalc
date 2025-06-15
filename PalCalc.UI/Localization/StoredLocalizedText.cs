using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public class StoredLocalizedText : ILocalizedText
    {
        private StoredLocalizableText src;

        private Dictionary<string, object> formatArgs;

        public StoredLocalizedText(StoredLocalizableText src, Dictionary<string, object> formatArgs)
        {
            this.src = src;
            this.formatArgs = formatArgs;
        }

        public override string Value
        {
            get
            {
                lock (Translator.ItlCodeUsage)
                    Translator.ItlCodeUsage[src.Code] += 1;

                if (Translator.DEBUG_DISABLE_TRANSLATIONS)
#pragma warning disable CS0162 // Unreachable code detected
                    return src.Code.ToString();
#pragma warning restore CS0162 // Unreachable code detected

                var result = src.BaseLocalizedText;
                foreach (var p in src.Parameters)
                {
                    var rawArgVal = formatArgs[p];
                    var argText = rawArgVal is ILocalizedText
                        ? (rawArgVal as ILocalizedText).Value
                        : rawArgVal.ToString();

                    result = result.Replace("{" + p + "}", argText);
                }
                return result;
            }
        }
    }
}
