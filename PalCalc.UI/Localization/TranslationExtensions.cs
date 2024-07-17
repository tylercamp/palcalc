using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public static class TranslationExtensions
    {
        public static ILocalizedText Bind(this LocalizationCodes code) => code.Bind([]);

        public static ILocalizedText Bind(this LocalizationCodes code, Dictionary<string, object> formatArgs) =>
            Translator.Translations[code].Bind(formatArgs);

        public static ILocalizedText Bind(this LocalizationCodes code, object namedParams)
        {
            var properties = namedParams.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Dictionary<string, object> toPropertiesDict() => properties.ToDictionary(p => p.Name, p => p.GetValue(namedParams));

            var translation = Translator.Translations[code];

            if (translation.Parameters.Count == 1)
            {
                var param = translation.Parameters.Single();
                if (properties.Length == 1 && properties.Any(p => p.Name == param)) return translation.Bind(toPropertiesDict());
                else return translation.Bind(new Dictionary<string, object>() { { param, namedParams } });
            }
            else
            {
                return translation.Bind(toPropertiesDict());
            }
        }
    }
}
