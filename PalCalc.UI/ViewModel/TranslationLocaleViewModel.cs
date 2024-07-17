using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    public class TranslationLocaleViewModel : ObservableObject
    {
        public TranslationLocaleViewModel(TranslationLocale locale)
        {
            Value = locale;
            Label = Translator.Localizations[locale][LocalizationCodes.LC_ITL_LABEL];

            Translator.LocaleUpdated += () => OnPropertyChanged(nameof(IsSelected));

            SelectCommand = new RelayCommand(() => Translator.CurrentLocale = Value);
        }

        public TranslationLocale Value { get; }

        public string Label { get; }

        public bool IsSelected
        {
            get => Translator.CurrentLocale == Value;
            set
            {
                if (value == IsSelected) return;

                Translator.CurrentLocale = Value;
                MessageBox.Show(LocalizationCodes.LC_LANG_CHANGED_RESTART.Bind().Value);
            }
        }

        public IRelayCommand SelectCommand { get; }
    }
}
