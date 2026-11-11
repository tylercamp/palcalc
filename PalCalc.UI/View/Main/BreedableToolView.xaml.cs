using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        // a new run hides the cards while it loads, so start it back at the top
        private void Groups_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            ResetScroll(GroupsContents, GroupsScrollViewer);

        // (also used by `ParentCombosToolView`)
        internal static void ResetScroll(ListBox groupsContents, ScrollViewer groupsScrollViewer)
        {
            if (groupsScrollViewer.IsVisible) return;

            groupsContents.SelectedItem = null;
            groupsScrollViewer.ScrollToTop();
        }

        private void GroupsContents_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            ScrollToGroup(GroupsList, GroupsScrollViewer, GroupsContents.SelectedItem);

        private void GroupsContents_ItemClicked(object sender, MouseButtonEventArgs e) =>
            ScrollToGroup(GroupsList, GroupsScrollViewer, ((FrameworkElement)sender).DataContext);

        // scrolls so that the group's header is at the top, or as close as it gets for the last few groups.
        // (also used by `ParentCombosToolView`, which has the same layout)
        internal static void ScrollToGroup(ItemsControl groupsList, ScrollViewer groupsScrollViewer, object group)
        {
            if (group == null || groupsList.ItemContainerGenerator.ContainerFromItem(group) is not FrameworkElement container) return;

            var top = container.TransformToAncestor(groupsList).Transform(new Point(0, 0)).Y;
            groupsScrollViewer.ScrollToVerticalOffset(top);
        }
    }
}
