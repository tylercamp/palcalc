using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using DotNetKit.Misc.Disposables;
using DotNetKit.Windows.Media;

namespace DotNetKit.Windows.Controls
{
    /// <summary>
    /// AutoCompleteComboBox.xaml
    /// </summary>
    public partial class AutoCompleteComboBox : ComboBox
    {
        readonly SerialDisposable disposable = new SerialDisposable();

        TextBox editableTextBoxCache;

        Predicate<object> defaultItemsFilter;

        object lastValidSelectedItem;

        public AutoCompleteComboBox()
        {
            InitializeComponent();

            AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
        }

        public TextBox EditableTextBox
        {
            get
            {
                if (editableTextBoxCache == null)
                {
                    const string name = "PART_EditableTextBox";
                    editableTextBoxCache = (TextBox)VisualTreeModule.FindChild(this, name);
                }
                return editableTextBoxCache;
            }
        }

        /// <summary>
        /// Gets text to match with the query from an item.
        /// Never null.
        /// </summary>
        /// <param name="item"/>
        string TextFromItem(object item)
        {
            if (item == null) return string.Empty;

            var d = new DependencyVariable<string>();
            d.SetBinding(item, TextSearch.GetTextPath(this));
            return d.Value ?? string.Empty;
        }

        #region ItemsSource
        public static new readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(AutoCompleteComboBox),
                new PropertyMetadata(null, ItemsSourcePropertyChanged));
        public new IEnumerable ItemsSource
        {
            get
            {
                return (IEnumerable)GetValue(ItemsSourceProperty);
            }
            set
            {
                SetValue(ItemsSourceProperty, value);
            }
        }

        private static void ItemsSourcePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dpcea)
        {
            var comboBox = (ComboBox)dependencyObject;
            var previousSelectedItem = comboBox.SelectedItem;

            if (dpcea.NewValue is ICollectionView cv)
            {
                ((AutoCompleteComboBox)dependencyObject).defaultItemsFilter = cv.Filter;
                comboBox.ItemsSource = cv;
            }
            else
            {
                ((AutoCompleteComboBox)dependencyObject).defaultItemsFilter = null;
                IEnumerable newValue = dpcea.NewValue as IEnumerable;
                CollectionViewSource newCollectionViewSource = new CollectionViewSource
                {
                    Source = newValue
                };
                comboBox.ItemsSource = newCollectionViewSource.View;
            }

            // avoid unnecessary event re-raise
            if (comboBox.SelectedItem != previousSelectedItem)
                comboBox.SelectedItem = previousSelectedItem;

            // if ItemsSource doesn't contain previousSelectedItem
            if (comboBox.SelectedItem != previousSelectedItem)
            {
                comboBox.SelectedItem = null;
            }

            ((AutoCompleteComboBox)dependencyObject).lastValidSelectedItem = comboBox.SelectedItem;
        }
        #endregion ItemsSource

        #region Setting
        static readonly DependencyProperty settingProperty =
            DependencyProperty.Register(
                "Setting",
                typeof(AutoCompleteComboBoxSetting),
                typeof(AutoCompleteComboBox)
            );

        public static DependencyProperty SettingProperty
        {
            get { return settingProperty; }
        }

        public AutoCompleteComboBoxSetting Setting
        {
            get { return (AutoCompleteComboBoxSetting)GetValue(SettingProperty); }
            set { SetValue(SettingProperty, value); }
        }

        AutoCompleteComboBoxSetting SettingOrDefault
        {
            get { return Setting ?? AutoCompleteComboBoxSetting.Default; }
        }
        #endregion

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            if (SelectedItem != null)
                lastValidSelectedItem = SelectedItem;
        }

        #region OnTextChanged
        long revisionId;
        string previousText;

        struct TextBoxStatePreserver
            : IDisposable
        {
            readonly TextBox textBox;
            readonly int selectionStart;
            readonly int selectionLength;
            readonly string text;

            public void Dispose()
            {
                textBox.Text = text;
                textBox.Select(selectionStart, selectionLength);
            }

            public TextBoxStatePreserver(TextBox textBox)
            {
                this.textBox = textBox;
                selectionStart = textBox.SelectionStart;
                selectionLength = textBox.SelectionLength;
                text = textBox.Text;
            }
        }

        static int CountWithMax<T>(IEnumerable<T> xs, Predicate<T> predicate, int maxCount)
        {
            var count = 0;
            foreach (var x in xs)
            {
                if (predicate(x))
                {
                    count++;
                    if (count > maxCount) return count;
                }
            }
            return count;
        }

        void Unselect()
        {
            var textBox = EditableTextBox;
            textBox.Select(textBox.SelectionStart + textBox.SelectionLength, 0);
        }

        void UpdateFilter(Predicate<object> filter)
        {
            using (new TextBoxStatePreserver(EditableTextBox))
            using (Items.DeferRefresh())
            {
                // Can empty the text box. I don't why.
                Items.Filter = filter;
            }
        }

        void OpenDropDown(Predicate<object> filter)
        {
            UpdateFilter(filter);
            IsDropDownOpen = true;
            Unselect();
        }

        void OpenDropDown()
        {
            var filter = GetFilter();
            OpenDropDown(filter);
        }

        void UpdateSuggestionList()
        {
            var text = Text;

            if (text == previousText) return;
            previousText = text;

            if (string.IsNullOrEmpty(text))
            {
                IsDropDownOpen = false;
                SelectedItem = null;

                using (Items.DeferRefresh())
                {
                    Items.Filter = defaultItemsFilter;
                }
            }
            else if (SelectedItem != null && TextFromItem(SelectedItem) == text)
            {
                // It seems the user selected an item.
                // Do nothing.
            }
            else
            {
                using (new TextBoxStatePreserver(EditableTextBox))
                {
                    SelectedItem = null;
                }

                var filter = GetFilter();
                var maxCount = SettingOrDefault.MaxSuggestionCount;
                var count = CountWithMax(ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>(), filter, maxCount);

                if (0 < count && count <= maxCount)
                {
                    OpenDropDown(filter);
                }
            }
        }

        void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var id = unchecked(++revisionId);
            var setting = SettingOrDefault;

            if (setting.Delay <= TimeSpan.Zero)
            {
                UpdateSuggestionList();
                return;
            }

            var matchingItem = ItemsSource.Cast<object>().Where(i => TextFromItem(i) == Text).FirstOrDefault();
            if (matchingItem != SelectedItem)
            {
                using (new TextBoxStatePreserver(EditableTextBox))
                {
                    SelectedItem = matchingItem;
                }
            }

            disposable.Content =
                new Timer(
                    state =>
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            if (revisionId != id) return;
                            UpdateSuggestionList();
                        });
                    },
                    null,
                    setting.Delay,
                    Timeout.InfiniteTimeSpan
                );
        }
        #endregion

        bool SelectItemFromMatchingText(string text)
        {
            if (ItemsSource == null) return false;

            var filter = GetFilter(text);
            var matchingItems = ItemsSource.Cast<object>().Where(filter.Invoke).ToList();
            if (matchingItems.Count == 1) SelectedItem = matchingItems.Single();

            return matchingItems.Count == 1;
        }

        void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ItemsSource == null) return;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Space)
            {
                OpenDropDown();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Text = lastValidSelectedItem == null ? "" : TextFromItem(lastValidSelectedItem);
                SelectedItem = lastValidSelectedItem;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (Text == "")
                {
                    SelectedItem = null;
                    lastValidSelectedItem = null;
                }
                else if (!SelectItemFromMatchingText(Text))
                {
                    Text = lastValidSelectedItem == null ? "" : TextFromItem(lastValidSelectedItem);
                    SelectedItem = lastValidSelectedItem;
                }
                e.Handled = true;
            }
        }

        void ComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ItemsSource == null) return;

            if (SettingOrDefault.ResetOnInvalidWhenFocusLost && (SelectedItem == null || TextFromItem(SelectedItem) != Text) && Text != "")
            {
                if (!SelectItemFromMatchingText(Text))
                {
                    SelectedItem = lastValidSelectedItem;
                    Text = lastValidSelectedItem == null
                        ? ""
                        : TextFromItem(lastValidSelectedItem);
                }
            }

            lastValidSelectedItem = SelectedItem;
        }

        Predicate<object> GetFilter(string forText = null)
        {
            var filter = SettingOrDefault.GetFilter(forText ?? Text, TextFromItem);

            return defaultItemsFilter != null
                ? i => defaultItemsFilter(i) && filter(i)
                : filter;
        }
    }
}