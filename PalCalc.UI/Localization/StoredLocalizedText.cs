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
                if (Translator.DEBUG_DISABLE_TRANSLATIONS)
                    return src.Code.ToString();

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
