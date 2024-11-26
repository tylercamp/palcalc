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

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for PalTargetView.xaml
    /// </summary>
    public partial class PalTargetView : StackPanel
    {
        public PalTargetView()
        {
            InitializeComponent();
        }

        private void PresetsButton_Click(object sender, RoutedEventArgs e)
        {
            PresetsPopup.IsOpen = true;
            PresetsPopup.Focus();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PresetsPopup.IsOpen = false;
        }
    }
}
