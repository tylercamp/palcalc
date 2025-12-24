using AdonisUI.Controls;
using PalCalc.UI.ViewModel;
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
using System.Windows.Shapes;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for PassivesCheckListWindow.xaml
    /// </summary>
    public partial class PassivesCheckListWindow : AdonisWindow
    {
        public PassivesCheckListWindow()
        {
            InitializeComponent();
        }

        // Note - PalCheckListWindow copy/pasta

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var senderElem = sender as FrameworkElement;
            var senderVm = senderElem.DataContext as PassivesCheckListEntryViewModel;

            senderVm.IsEnabled = !senderVm.IsEnabled;
        }

        private void m_ListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                var items = m_ListView.SelectedItems.OfType<PassivesCheckListEntryViewModel>();
                var shouldEnable = items.Any(i => !i.IsEnabled);

                foreach (var item in items)
                {
                    item.IsEnabled = shouldEnable;
                }

                e.Handled = true;
            }
        }
    }
}
