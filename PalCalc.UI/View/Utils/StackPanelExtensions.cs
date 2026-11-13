using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace PalCalc.UI.View.Utils
{
    // ty chatgpt
    public static class StackPanelExtensions
    {
        private static readonly ConditionalWeakTable<StackPanel, Action> stopTrackingItems = new();

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
            if (d is not StackPanel panel) return;

            ApplySpacing(panel);

            if ((double)e.NewValue == 0)
            {
                panel.Loaded -= PanelLoaded;
                panel.Unloaded -= PanelUnloaded;
                PanelUnloaded(panel, null);
            }
            else if ((double)e.OldValue == 0)
            {
                panel.Loaded += PanelLoaded;
                panel.Unloaded += PanelUnloaded;
                if (panel.IsLoaded)
                    PanelLoaded(panel, null);
            }
        }

        private static void PanelLoaded(object sender, RoutedEventArgs e)
        {
            var panel = (StackPanel)sender;
            ApplySpacing(panel);

            if (ItemsControl.GetItemsOwner(panel)?.ItemContainerGenerator is not IItemContainerGenerator ownerGenerator)
                return;

            var generator = ownerGenerator.GetItemContainerGeneratorForPanel(panel);
            if (generator == null || stopTrackingItems.TryGetValue(panel, out _)) return;

            DispatcherOperation pendingUpdate = null;
            void ItemsChanged(object _, ItemsChangedEventArgs args)
            {
                if (pendingUpdate != null) return;

                // Let WPF finish updating the children before we set their margins.
                pendingUpdate = panel.Dispatcher.InvokeAsync(() =>
                {
                    pendingUpdate = null;
                    ApplySpacing(panel);
                }, DispatcherPriority.DataBind);
            }

            generator.ItemsChanged += ItemsChanged;
            stopTrackingItems.Add(panel, () =>
            {
                generator.ItemsChanged -= ItemsChanged;
                pendingUpdate?.Abort();
            });
        }

        private static void PanelUnloaded(object sender, RoutedEventArgs e)
        {
            var panel = (StackPanel)sender;
            if (stopTrackingItems.TryGetValue(panel, out var stop))
            {
                stopTrackingItems.Remove(panel);
                stop();
            }
        }

        private static void ApplySpacing(StackPanel panel)
        {
            double spacing = GetSpacing(panel);
            var gap = panel.Orientation == Orientation.Horizontal
                ? new Thickness(spacing, 0, 0, 0)
                : new Thickness(0, spacing, 0, 0);
            var margin = new Thickness(0);
            foreach (var child in panel.Children.OfType<FrameworkElement>().ToList())
            {
                child.Margin = margin;
                margin = gap;
            }
        }
    }

}
