using PalCalc.UI.View;
using PalCalc.UI.ViewModel;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace PalCalc.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private ILogger logger = Log.ForContext<MainPage>();

        internal MainPage()
        {
            InitializeComponent();

            var sw = Stopwatch.StartNew();
            DataContext = new MainWindowViewModel(Dispatcher);
            logger.Information("MainWindowViewModel took {ms}ms to start", sw.ElapsedMilliseconds);
        }

        internal MainPage(MainWindowViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

        private void SolverControlsView_OnRun()
        {
            ViewModel.RunSolver();
        }

        private void SolverControlsView_OnCancel()
        {
            ViewModel.CancelSolver();
        }

        //protected override void OnClosing(CancelEventArgs e)
        //{
        //    base.OnClosing(e);

        //    ViewModel.CancelSolver();
        //}

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AboutWindow();
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
        }

        private void DownloadUpdateLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ViewModel.TryDownloadLatestVersion();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.ToString(), UseShellExecute = true });
        }
    }
}
