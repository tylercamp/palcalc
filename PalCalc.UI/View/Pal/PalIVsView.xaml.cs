using PalCalc.Solver;
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

namespace PalCalc.UI.View.Pal
{
    /// <summary>
    /// Interaction logic for PalIVsView.xaml
    /// </summary>
    public partial class PalIVsView : UserControl
    {
        public PalIVsView()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.RegisterAttached(nameof(Orientation), typeof(Orientation), typeof(PalIVsView), new PropertyMetadata(Orientation.Vertical));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty IV_HPProperty = DependencyProperty.Register(nameof(IV_HP), typeof(IVValueViewModel), typeof(PalIVsView));

        public IVValueViewModel IV_HP
        {
            get => GetValue(IV_HPProperty) as IVValueViewModel;
            set => SetValue(IV_HPProperty, value);
        }

        public static readonly DependencyProperty IV_AttackProperty = DependencyProperty.Register(nameof(IV_Attack), typeof(IVValueViewModel), typeof(PalIVsView));

        public IVValueViewModel IV_Attack
        {
            get => GetValue(IV_AttackProperty) as IVValueViewModel;
            set => SetValue(IV_AttackProperty, value);
        }

        public static readonly DependencyProperty IV_DefenseProperty = DependencyProperty.Register(nameof(IV_Defense), typeof(IVValueViewModel), typeof(PalIVsView));

        public IVValueViewModel IV_Defense
        {
            get => GetValue(IV_DefenseProperty) as IVValueViewModel;
            set => SetValue(IV_AttackProperty, value);
        }
    }
}
