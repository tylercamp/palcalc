using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for ParentCombosToolView.xaml
    /// </summary>
    public partial class ParentCombosToolView : UserControl
    {
        public ParentCombosToolView()
        {
            InitializeComponent();
        }

        // (same groups layout as `BreedableToolView`, see there for why)
        private void Groups_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            BreedableToolView.ResetScroll(GroupsContents, GroupsScrollViewer);

        private void GroupsContents_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            BreedableToolView.ScrollToGroup(GroupsList, GroupsScrollViewer, GroupsContents.SelectedItem);

        private void GroupsContents_ItemClicked(object sender, MouseButtonEventArgs e) =>
            BreedableToolView.ScrollToGroup(GroupsList, GroupsScrollViewer, ((FrameworkElement)sender).DataContext);
    }
}
