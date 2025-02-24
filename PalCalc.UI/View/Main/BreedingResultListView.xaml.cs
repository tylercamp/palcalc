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

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for BreedingResultListView.xaml
    /// </summary>
    public partial class BreedingResultListView : ListView
    {
        public BreedingResultListView()
        {
            InitializeComponent();

            SetResourceReference(StyleProperty, typeof(ListView));

            Wpf.Util.GridViewSort.ApplySort(Items, nameof(BreedingResultViewModel.TimeEstimate));
        }
    }
}
