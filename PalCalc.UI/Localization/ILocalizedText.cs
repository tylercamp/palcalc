using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public abstract partial class ILocalizedText : ObservableObject
    {
        [NotifyPropertyChangedFor(nameof(Value))]
        [ObservableProperty]
        private TranslationLocale locale;

        public abstract string Value { get; }
    }
}
