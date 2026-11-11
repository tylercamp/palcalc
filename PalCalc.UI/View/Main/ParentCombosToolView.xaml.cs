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
        private void Groups_LayoutChanged(object sender, RoutedEventArgs e) =>
            Dispatcher.BeginInvoke(() => BreedableToolView.UpdateScrollBar(GroupsList, GroupsScrollViewer));

        private void Groups_PreviewMouseWheel(object sender, MouseWheelEventArgs e) => BreedableToolView.ScrollGroups(sender, e);

        private void Groups_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            Dispatcher.BeginInvoke(() => BreedableToolView.UpdateScrollBar(GroupsList, GroupsScrollViewer));
    }
}
