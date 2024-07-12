using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Inspector.Search;
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

namespace PalCalc.UI.View.Inspector
{
    /// <summary>
    /// Interaction logic for SearchView.xaml
    /// </summary>
    public partial class SearchView : UserControl
    {
        public SearchView()
        {
            InitializeComponent();
        }

        public SearchViewModel ViewModel => DataContext as SearchViewModel;

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ViewModel == null) return;

            ViewModel.OwnerTree.SelectedNode = e.NewValue as IOwnerTreeNode;
        }
    }
}
