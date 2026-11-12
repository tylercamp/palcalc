using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for BreedableToolView.xaml
    /// </summary>
    public partial class BreedableToolView : UserControl
    {
        private readonly TopCardAnchor topCardAnchor;

        public BreedableToolView()
        {
            InitializeComponent();
            topCardAnchor = new TopCardAnchor(this, GroupsList, GroupsScrollViewer);
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

    /// <summary>
    /// Keeps the card at the top of the view in place when Show IVs changes the cards' heights, so the cards
    /// below it move down instead of the view landing on different cards. Also used by `ParentCombosToolView`.
    /// </summary>
    internal class TopCardAnchor
    {
        private readonly ItemsControl groupsList;
        private readonly ScrollViewer scrollViewer;

        public TopCardAnchor(FrameworkElement view, ItemsControl groupsList, ScrollViewer scrollViewer)
        {
            this.groupsList = groupsList;
            this.scrollViewer = scrollViewer;

            // (weak events, since the tool outlives its windows and we don't want it keeping closed ones alive)
            view.DataContextChanged += (_, e) =>
            {
                if (e.OldValue is INotifyPropertyChanged oldTool)
                    PropertyChangedEventManager.RemoveHandler(oldTool, OnShowIVsChanged, "ShowIVs");

                if (e.NewValue is INotifyPropertyChanged newTool)
                    PropertyChangedEventManager.AddHandler(newTool, OnShowIVsChanged, "ShowIVs");
            };
        }

        // the cards haven't been laid out at their new heights when this runs, so we can still read where they were
        private void OnShowIVsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!scrollViewer.IsVisible || TopCard() is not (FrameworkElement card, double cardOffset)) return;

            // the cards' own IV bindings might not have heard about the change yet, so we wait for them. Normal
            // priority still runs before the next layout and render, so there's never a frame where the view jumps
            scrollViewer.Dispatcher.BeginInvoke(() =>
            {
                scrollViewer.UpdateLayout();
                if (card.IsVisible)
                    scrollViewer.ScrollToVerticalOffset(TopOf(card) - cardOffset);
            }, DispatcherPriority.Normal);
        }

        // the first card that's still showing at the top of the view, and how far its top is from the view's top
        private (FrameworkElement, double)? TopCard()
        {
            var offset = scrollViewer.VerticalOffset;
            for (int g = 0; g < groupsList.Items.Count; g++)
            {
                if (groupsList.ItemContainerGenerator.ContainerFromIndex(g) is not FrameworkElement group || TopOf(group) + group.ActualHeight <= offset)
                    continue;

                var cards = FindCards(group);
                for (int c = 0; cards != null && c < cards.Items.Count; c++)
                {
                    if (cards.ItemContainerGenerator.ContainerFromIndex(c) is not FrameworkElement container) continue;

                    var top = TopOf(container);
                    if (top + container.ActualHeight > offset)
                        return (container, top - offset);
                }
            }
            return null;
        }

        private double TopOf(FrameworkElement element) => element.TransformToAncestor(groupsList).Transform(new Point(0, 0)).Y;

        // the group's own list of cards, inside its container
        private static ItemsControl FindCards(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ItemsControl cards) return cards;

                var found = FindCards(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
