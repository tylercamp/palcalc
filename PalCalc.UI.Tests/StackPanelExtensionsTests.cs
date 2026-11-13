using PalCalc.UI.View.Utils;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PalCalc.UI.Tests;

[TestClass]
public class StackPanelExtensionsTests
{
    [STATestMethod]
    public void SpacingFollowsGeneratedItems()
    {
        var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
        panelFactory.SetValue(StackPanelExtensions.SpacingProperty, 5.0);
        var control = new ItemsControl
        {
            Template = new ControlTemplate(typeof(ItemsControl))
            {
                VisualTree = new FrameworkElementFactory(typeof(ItemsPresenter)),
            },
            ItemsPanel = new ItemsPanelTemplate(panelFactory),
        };
        var size = new Size(400, 400);
        control.Measure(size);
        control.Arrange(new Rect(size));
        var presenter = VisualTreeHelper.GetChild(control, 0);
        var panel = (StackPanel)VisualTreeHelper.GetChild(presenter, 0);
        panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        control.ItemsSource = new[] { "first", "second" };
        AssertMargins(new Thickness(0), new Thickness(0, 5, 0, 0));

        var replacement = new ObservableCollection<string> { "third", "fourth" };
        control.ItemsSource = Array.Empty<string>();
        control.ItemsSource = replacement;
        AssertMargins(new Thickness(0), new Thickness(0, 5, 0, 0));

        replacement.Move(0, 1);
        AssertMargins(new Thickness(0), new Thickness(0, 5, 0, 0));
        replacement.RemoveAt(0);
        AssertMargins(new Thickness(0));

        replacement.Insert(0, "fifth");
        panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        AssertMargins(new Thickness(0), new Thickness(0));
        panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        AssertMargins(new Thickness(0), new Thickness(0, 5, 0, 0));
        replacement.RemoveAt(0);
        AssertMargins(new Thickness(0));
        panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        void AssertMargins(params Thickness[] expected)
        {
            panel.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            CollectionAssert.AreEqual(expected, panel.Children.Cast<FrameworkElement>().Select(child => child.Margin).ToArray());
        }
    }
}
