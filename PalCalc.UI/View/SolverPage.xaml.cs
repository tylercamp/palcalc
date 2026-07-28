using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

namespace PalCalc.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class SolverPage : UserControl
    {
        internal SolverPage()
        {
            InitializeComponent();
            WindowStatePersistence.AttachGrid(this, "solver", () => Columns, () => Rows);

            DataContext = new SolverPageViewModel(Dispatcher, null, null, null);
        }

        internal SolverPage(SolverPageViewModel vm)
        {
            InitializeComponent();
            WindowStatePersistence.AttachGrid(this, "solver", () => Columns, () => Rows);
            DataContext = vm;
        }

        private SolverPageViewModel ViewModel => DataContext as SolverPageViewModel;

        private IReadOnlyList<ColumnDefinition> Columns => new[] { LeftColumn, CenterColumn, RightColumn };
        private IReadOnlyList<RowDefinition> Rows => new[] { SourcePalsRow, ResultsRow };

        // Default sizes from SolverPage.xaml — used by the Reset Window Layout action.
        public void ResetLayout()
        {
            LeftColumn.Width = new GridLength(300);
            CenterColumn.Width = new GridLength(232, GridUnitType.Star);
            RightColumn.Width = new GridLength(80, GridUnitType.Star);
            SourcePalsRow.Height = new GridLength(30, GridUnitType.Star);
            ResultsRow.Height = new GridLength(60, GridUnitType.Star);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.ToString(), UseShellExecute = true });
        }
    }
}
