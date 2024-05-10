using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for IntegerTextBox.xaml
    /// </summary>
    public partial class IntegerTextBox : UserControl
    {
        public IntegerTextBox()
        {
            InitializeComponent();

            textBox.Text = Value.ToString();
        }

        public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register(
            nameof(MinValue), typeof(int), typeof(IntegerTextBox),
            new PropertyMetadata(int.MinValue)
        );
        public int MinValue
        {
            get => (int)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
            nameof(MaxValue), typeof(int), typeof(IntegerTextBox),
            new PropertyMetadata(int.MaxValue)
        );
        public int MaxValue
        {
            get => (int)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(IntegerTextBox),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ValueChanged, CoerceValue, false, UpdateSourceTrigger.PropertyChanged)
        );
        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private static object CoerceValue(DependencyObject d, object value)
        {
            var itb = d as IntegerTextBox;
            var v = (int)value;

            if (v < itb.MinValue) return itb.MinValue;
            if (v > itb.MaxValue) return itb.MaxValue;
            return v;
        }

        private static void ValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var itb = d as IntegerTextBox;
            var newValue = (int)e.NewValue;

            if (newValue < itb.MinValue)
            {
                itb.Value = itb.MinValue;
            }
            else if (newValue > itb.MaxValue)
            {
                itb.Value = itb.MaxValue;
            }
            else
            {
                var valueText = e.NewValue.ToString();
                if (itb.textBox.Text != valueText) itb.textBox.Text = valueText;
                itb.lastValidText = valueText;
            }
        }

        private string lastValidText = ValueProperty.DefaultMetadata.DefaultValue.ToString();

        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(textBox.Text, out int newValue))
            {
                if (MinValue >= 0 && newValue < 0)
                {
                    textBox.Text = lastValidText;
                }
                else
                {
                    Value = newValue;
                    if (Value != newValue) textBox.Text = lastValidText;
                }
            }
            else
            {
                textBox.Text = lastValidText;
            }
        }

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.Up) Value += 1;
            else if (e.Key == Key.Down) Value -= 1;
            else e.Handled = false;
        }

        private void textBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!textBox.IsFocused)
            {
                textBox.Focus();
                textBox.SelectAll();
                e.Handled = true;
            }
        }

        private void textBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            textBox_KeyDown(sender, e);
        }
    }
}
