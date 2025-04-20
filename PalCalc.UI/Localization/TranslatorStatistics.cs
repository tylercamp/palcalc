using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public static partial class Translator
    {
        private static Dictionary<LocalizationCodes, int> itlCodeUsage = null;
        internal static Dictionary<LocalizationCodes, int> ItlCodeUsage => itlCodeUsage ??= Enum.GetValues<LocalizationCodes>().ToDictionary(c => c, c => 0);

        public static T WithCodeUsage<T>(Func<Dictionary<LocalizationCodes, int>, T> fn)
        {
            Dictionary<LocalizationCodes, int> counts;
            lock (ItlCodeUsage)
                counts = ItlCodeUsage;

            return fn(counts);
        }
    }
}
