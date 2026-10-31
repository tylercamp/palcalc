using System.Windows.Controls;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for ToolListView.xaml
    /// </summary>
    public partial class ToolListView : ListBox
    {
        public ToolListView()
        {
            InitializeComponent();

            SetResourceReference(StyleProperty, typeof(ListBox));
        }
    }
}
