using PalCalc.UI.ViewModel.Mapped;
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
    /// Interaction logic for PalSpecifierView.xaml
    /// </summary>
    public partial class PalSpecifierView : UserControl
    {
        public PalSpecifierView()
        {
            InitializeComponent();
        }

        public PalSpecifierViewModel ViewModel => DataContext as PalSpecifierViewModel;

        private void Image_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.DeleteCommand?.Execute(ViewModel);
            e.Handled = true;
        }
    }
}
