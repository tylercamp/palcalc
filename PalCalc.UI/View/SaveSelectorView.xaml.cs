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

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for SaveSelectorView.xaml
    /// </summary>
    public partial class SaveSelectorView : StackPanel
    {
        public SaveSelectorView()
        {
            InitializeComponent();
        }

        private SaveSelectorViewModel ViewModel => DataContext as SaveSelectorViewModel;

        private void SavesLocationsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedLocation == null) return;

            var location = ViewModel?.SelectedLocation as StandardSavesLocationViewModel;
            if (location == null) return;

            var fullPath = System.IO.Path.GetFullPath(location.Value.FolderPath);
            Process.Start("explorer.exe", fullPath);
        }

        private void SaveGameFolder_Click(object sender, RoutedEventArgs e)
        {
            var saveGame = ViewModel?.SelectedGame?.Value;
            if (saveGame == null) return;

            var fullPath = System.IO.Path.GetFullPath(saveGame.BasePath);
            Process.Start("explorer.exe", fullPath);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
        }
    }
}
