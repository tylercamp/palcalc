using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Solver;
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
using System.Windows.Threading;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for PalSourceTreeView.xaml
    /// </summary>
    public partial class PalSourceTreeView : UserControl
    {
        public PalSourceTreeView()
        {
            InitializeComponent();
        }

        public PalSourceTreeViewModel ViewModel => DataContext as PalSourceTreeViewModel;


        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ViewModel == null) return;

            if (e.NewValue != null && e.NewValue is not IPalSourceTreeNode) throw new InvalidOperationException();

            ViewModel.SelectedNode = e.NewValue as IPalSourceTreeNode;
        }
    }
}
