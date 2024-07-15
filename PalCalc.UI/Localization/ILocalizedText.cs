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

        public override bool Equals(object obj) => obj?.GetHashCode() == GetHashCode();
        public override int GetHashCode() => HashCode.Combine(this.GetType(), Value);

        public override string ToString() => throw new Exception();
    }
}
