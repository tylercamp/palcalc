using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.View.Utils
{
    // ty chatgpt
    public static class StackPanelExtensions
    {
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.RegisterAttached(
                "Spacing",
                typeof(double),
                typeof(StackPanelExtensions),
                new PropertyMetadata(0.0, OnSpacingChanged));

        public static void SetSpacing(DependencyObject element, double value)
            => element.SetValue(SpacingProperty, value);

        public static double GetSpacing(DependencyObject element)
            => (double)element.GetValue(SpacingProperty);

        private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Panel panel)
            {
                // Apply spacing initially
                ApplySpacing(panel);

                // Re-apply if children change or layout updates
                panel.Loaded += (s, _) => ApplySpacing(panel);
            }
        }

        private static void ApplySpacing(Panel panel)
        {
            double spacing = GetSpacing(panel);

            if (panel is StackPanel stackPanel)
            {
                var children = stackPanel.Children.OfType<FrameworkElement>().ToList();
                for (int i = 0; i < children.Count; i++)
                {
                    // Reset margin first (optional, if you want to reset existing margins)
                    children[i].Margin = new Thickness(0);

                    if (i > 0)
                    {
                        if (stackPanel.Orientation == Orientation.Horizontal)
                            children[i].Margin = new Thickness(spacing, 0, 0, 0);
                        else
                            children[i].Margin = new Thickness(0, spacing, 0, 0);
                    }
                }
            }
        }
    }

}
