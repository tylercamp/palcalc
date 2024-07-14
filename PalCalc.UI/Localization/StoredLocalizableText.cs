using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public class StoredLocalizableText : ILocalizableText
    {
        public StoredLocalizableText(string id)
        {
            Id = id;

            Parameters = id.Split('|').Skip(1).Select(p => p.Trim()).ToList();
        }

        public string Id { get; }

        public List<string> Parameters { get; }

        public string BaseLocalizedText => Translator.Localizations[Locale][Id];


        public StoredLocalizedText Bind() => Bind([]);

        public StoredLocalizedText Bind(Dictionary<string, object> formatArgs)
        {
            var missing = Parameters.Except(formatArgs.Keys);
            if (missing.Any())
            {
                throw new Exception("Binding parameters are missing: " + string.Join(", ", missing));
            }

            var nullValues = formatArgs.Where(kvp => kvp.Value == null);
            if (nullValues.Any())
            {
                throw new Exception("Binding parameters are null: " + string.Join(", ", nullValues.Select(v => v.Key)));
            }

            // TODO - validate format args
            var res = new StoredLocalizedText(this, formatArgs) { Locale = Locale };
            Track(res);
            return res;
        }
    }
}
