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
    // (XAML util, see uses of `{itl:LocalizedText ITL_CODE)`
    public class LocalizedTextExtension : MarkupExtension
    {
        public LocalizationCodes Code
        {
            set => lt = value.Bind();
        }

        private ILocalizedText lt;

        public LocalizedTextExtension()
        {
        }

        public LocalizedTextExtension(LocalizationCodes code)
        {
            Code = code;
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
