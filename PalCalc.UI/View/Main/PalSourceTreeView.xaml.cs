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

        /* You can click on a checkbox and you can click on individual TreeViewItems, but the entry will
         * only be toggled by clicking on the checkbox
         * 
         * Capture TreeViewItem clicks and emulate that toggle behavior manually
         */

        private void TreeViewItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var tvi = sender as TreeViewItem;
            tvi.PreviewMouseUp += Tvi_PreviewMouseUp;
            tvi.CaptureMouse();

            e.Handled = true;
        }

        private void Tvi_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var tvi = sender as TreeViewItem;
            tvi.ReleaseMouseCapture();
            tvi.PreviewMouseUp -= Tvi_PreviewMouseUp;

            if (tvi.IsMouseOver)
            {
                var n = tvi.DataContext as IPalSourceTreeNode;
                if (n != null)
                {
                    switch (n.IsChecked)
                    {
                        case true: n.IsChecked = false; break;
                        case false: n.IsChecked = true; break;
                        case null: n.IsChecked = false; break;
                    }
                }
            }

            e.Handled = true;
        }

        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1)
                e.Handled = true;
        }
    }
}
