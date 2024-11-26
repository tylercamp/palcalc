using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Localization
{
    public abstract partial class ILocalizedText : ObservableObject, IComparable<ILocalizedText>, IComparable
    {
        [NotifyPropertyChangedFor(nameof(Value))]
        [ObservableProperty]
        private TranslationLocale locale;

        public abstract string Value { get; }

        public override bool Equals(object obj) => obj?.GetHashCode() == GetHashCode();
        public override int GetHashCode() => HashCode.Combine(this.GetType(), Value);

        public int CompareTo(ILocalizedText other) => Value.CompareTo(other.Value);

        // helpful for XAML bindings where we forgot to call `.Value`, this (usually) creates a XAML binding
        // error that you can just double-click on to find the source
        public override string ToString() => throw new Exception();

        public int CompareTo(object obj) => CompareTo(obj as ILocalizedText);
    }
}
