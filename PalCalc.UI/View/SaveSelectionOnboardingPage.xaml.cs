using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace PalCalc.UI.View
{
    public partial class SaveSelectionOnboardingPage : UserControl
    {
        public SaveSelectionOnboardingPage()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        }
    }
}
