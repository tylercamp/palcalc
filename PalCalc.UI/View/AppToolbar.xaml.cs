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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for AppToolbar.xaml
    /// </summary>
    public partial class AppToolbar : UserControl
    {
        public AppToolbar()
        {
            InitializeComponent();

#if DEBUG
            DebugMenuItem.Visibility = Visibility.Visible;
#else
            DebugMenuItem.Visibility = Visibility.Collapsed;
#endif
        }

        private void TranslationDebugger_Click(object sender, RoutedEventArgs e)
        {
            var debugWindow = new TranslationDebugWindow();
            debugWindow.DataContext = new TranslationDebugViewModel(App.TranslationErrors);
            debugWindow.Show();
        }
    }
}
