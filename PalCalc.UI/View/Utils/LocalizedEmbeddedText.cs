using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace PalCalc.UI.View.Utils
{
    public class EmbeddedParam : ContentControl
    {
        public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
            nameof(Key),
            typeof(string),
            typeof(EmbeddedParam),
            new PropertyMetadata(null)
        );

        public string Key
        {
            get => (string)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }
    }

    /// <summary>
    /// Used to display localized format-strings with UI elements instead of text for parameter values. e.g., by
    /// default you can easily reformat "Replace {Removed} with {Added}" using plain text, but this lets you replace
    /// `{Removed}` and `{Added}` with WPF controls instead of text.
    /// </summary>
    [ContentProperty(nameof(Params))]
    public class LocalizedEmbeddedText : Control
    {
        // ty chatgpt

        public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
            nameof(Code),
            typeof(LocalizationCodes),
            typeof(LocalizedEmbeddedText),
            new PropertyMetadata(default(LocalizationCodes), (d, _) => ((LocalizedEmbeddedText)d).Build())
        );

        public LocalizationCodes Code
        {
            get => (LocalizationCodes)GetValue(CodeProperty);
            set => SetValue(CodeProperty, value);
        }

        // child collection
        public ObservableCollection<EmbeddedParam> Params { get; } = new();

        private TextBlock _host;                // PART_TextBlock

        static LocalizedEmbeddedText() => DefaultStyleKeyProperty.OverrideMetadata(
                   typeof(LocalizedEmbeddedText),
                   new FrameworkPropertyMetadata(typeof(LocalizedEmbeddedText)));

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _host = GetTemplateChild("PART_TextBlock") as TextBlock;
            Build();
        }

        // Re-create the inline list every time Code or Displays changes
        private void Build()
        {
            if (_host == null) return;

            _host.Inlines.Clear();
            var dict = Params.ToDictionary(d => d.Key);

            var format = Translator.Translations[Code].BaseLocalizedText;

            foreach (var segment in Regex.Split(format, @"(\{[^}]+\})"))
            {
                if (segment.Length == 0) continue;

                if (segment.StartsWith("{") && segment.EndsWith("}"))
                {
                    var key = segment[1..^1];
                    if (dict.TryGetValue(key, out var el))
                        _host.Inlines.Add(new InlineUIContainer(el) { BaselineAlignment = BaselineAlignment.Center });
                    else
                        throw new Exception($"Unexpected param with key {key}");
                }
                else
                {
                    _host.Inlines.Add(new Run(segment));
                }
            }
        }

        // simple helpers to auto-refresh when children change
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Params.CollectionChanged += (_, __) => Build();
        }
    }
}
