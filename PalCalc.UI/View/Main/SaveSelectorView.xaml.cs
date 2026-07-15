using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for SaveSelectorView.xaml
    /// </summary>
    public partial class SaveSelectorView : StackPanel
    {
        public static readonly DependencyProperty AllowNavigationProperty = DependencyProperty.Register(nameof(AllowNavigation), typeof(bool), typeof(SaveSelectorView), new PropertyMetadata(true));

        public bool AllowNavigation
        {
            get => (bool)GetValue(AllowNavigationProperty);
            set => SetValue(AllowNavigationProperty, value);
        }

        public SaveSelectorView()
        {
            InitializeComponent();
        }
    }
}
