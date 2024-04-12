using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
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
    /// Interaction logic for BreedingResultView.xaml
    /// </summary>
    public partial class BreedingResultView : UserControl
    {
        public BreedingResultView()
        {
            InitializeComponent();
        }

        public BreedingResultViewModel ViewModel => DataContext as BreedingResultViewModel;
    }
}
