using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for TranslationDebugWindow.xaml
    /// </summary>
    public partial class TranslationDebugWindow : Window
    {
        private void CheckOwner()
        {
            if (Owner == null)
            {
                var mainWindow = App.Current.MainWindow;
                if (mainWindow.IsVisible)
                    Owner = mainWindow;
                else
                    Dispatcher.BeginInvoke(CheckOwner);
            }
        }

        public TranslationDebugWindow()
        {
            InitializeComponent();
            CheckOwner();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Owner = null;
            base.OnClosing(e);
        }
    }
}
