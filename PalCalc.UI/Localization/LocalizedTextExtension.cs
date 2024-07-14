using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace PalCalc.UI.Localization
{
    public class LocalizedTextExtension : MarkupExtension
    {
        public string Code { get; set; }
        public string Id => Translator.CodeToId[Code];

        private StoredLocalizedText lt;

        public LocalizedTextExtension(string code)
        {
            Code = code;
            lt = Translator.Translations[Id].Bind();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding(nameof(lt.Value))
            {
                Source = lt,
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
