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
using Theme.WPF.Themes;

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

        // TODO - refactor
        private void ChangeTheme(object sender, RoutedEventArgs e)
        {
            var newTheme = (((MenuItem)sender).Uid) switch
            {
                "0" => ThemeType.DeepDark,
                "1" => ThemeType.SoftDark,
                "2" => ThemeType.DarkGreyTheme,
                "3" => ThemeType.GreyTheme,
                "4" => ThemeType.LightTheme,
                "5" => ThemeType.RedBlackTheme,
                "6" => ThemeType.None,
                _ => throw new NotImplementedException()
            };

            if (!ThemesController.SetTheme(newTheme, forceTheme: false))
            {
                // TODO - itl
                if (MessageBox.Show("Pal Calc will restart to apply this theme.", "", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    AppSettings.Current.Theme = newTheme;
                    Storage.SaveAppSettings(AppSettings.Current);

                    App.Current.Exit += (o, e) =>
                    {
                        var p = Process.GetCurrentProcess();
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = p.MainModule.FileName,
                            WorkingDirectory = Directory.GetCurrentDirectory(),
                        });
                    };

                    App.Current.Shutdown(0);
                }
            }
            else
            {
                AppSettings.Current.Theme = newTheme;
                Storage.SaveAppSettings(AppSettings.Current);
            }

            e.Handled = true;
        }
    }
}
