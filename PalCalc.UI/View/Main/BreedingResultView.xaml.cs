using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
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
    /// Interaction logic for BreedingResultView.xaml
    /// </summary>
    public partial class BreedingResultView : UserControl
    {
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(BreedingResultView),
                new PropertyMetadata(false));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty DisplayedResultProperty =
            DependencyProperty.Register(
                nameof(DisplayedResult),
                typeof(BreedingResultViewModel),
                typeof(BreedingResultView),
                new PropertyMetadata(null));

        public BreedingResultViewModel DisplayedResult
        {
            get => (BreedingResultViewModel)GetValue(DisplayedResultProperty);
            set => SetValue(DisplayedResultProperty, value);
        }

        public BreedingResultView()
        {
            InitializeComponent();
        }
    }
}
