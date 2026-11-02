using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for BreedableToolView.xaml
    /// </summary>
    public partial class BreedableToolView : UserControl
    {
        public BreedableToolView()
        {
            InitializeComponent();
        }

        // (queued so that this only runs once after "Expand All" has changed every group, but still before the
        // first frame of their animations)
        private void Groups_LayoutChanged(object sender, RoutedEventArgs e) =>
            Dispatcher.BeginInvoke(UpdateScrollBar);

        // if the scrollbar only appeared after a group had grown, it would take up space and re-wrap the
        // cards at the end of the animation. decide on it now, from the height that the groups will end up at.
        private void UpdateScrollBar()
        {
            double height = 0;
            for (var i = 0; i < GroupsList.Items.Count; i++)
            {
                if (GroupsList.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter { IsLoaded: true } container) continue;

                var root = (FrameworkElement)VisualTreeHelper.GetChild(container, 0);
                var expander = (Expander)((Panel)root).Children[0];

                height += root.Margin.Bottom + PartHeight(expander, "HeaderSite");
                if (expander.IsExpanded) height += PartHeight(expander, "ExpandSite");
            }

            GroupsScrollViewer.VerticalScrollBarVisibility = height > GroupsScrollViewer.ViewportHeight
                ? ScrollBarVisibility.Visible
                : ScrollBarVisibility.Auto;
        }

        private static double PartHeight(Expander expander, string name) =>
            (expander.Template.FindName(name, expander) as FrameworkElement)?.ActualHeight ?? 0;

        // the template for `Expander` has its own `ScrollViewer` which never scrolls, but still eats
        // scroll events, so these need to be caught before they reach it
        private void Groups_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
