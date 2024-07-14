using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public partial class LocalizedText : ObservableObject
    {
        private LocalizableText src;

        private Dictionary<string, object> formatArgs;

        public LocalizedText(LocalizableText src, Dictionary<string, object> formatArgs)
        {
            this.src = src;
            this.formatArgs = formatArgs;
        }

        [NotifyPropertyChangedFor(nameof(Value))]
        [ObservableProperty]
        private TranslationLocale locale;

        public string Value
        {
            get
            {
                var result = src.BaseLocalizedText;
                foreach (var p in src.Parameters)
                    result = result.Replace("{" + p + "}", formatArgs[p].ToString());
                return result;
            }
        }

        public override string ToString() => $"{src.Id} => {Value}";
    }
}
