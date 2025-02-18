using DotNetKit.Windows.Controls;
using FuzzySharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    internal class AutoCompleteComboBoxSearchSettings : AutoCompleteComboBoxSetting
    {
        private AutoCompleteComboBoxSearchSettings() { }

        public static AutoCompleteComboBoxSearchSettings Instance { get; } = new();

        public override Predicate<object> GetFilter(string query, Func<object, string> stringFromItem)
        {
            return obj =>
            {
                var s = stringFromItem(obj);
                var weight = Fuzz.PartialRatio(query.ToLower(), stringFromItem(obj).ToLower());

                return stringFromItem(obj).Contains(query, StringComparison.InvariantCultureIgnoreCase) || weight > 80;
            };
        }
    }
}
