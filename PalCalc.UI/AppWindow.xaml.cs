using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.UI.ViewModel;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PalCalc.UI
{
    internal partial class AppWindowViewModel : ObservableObject
    {
        public AppWindowViewModel(Dispatcher dispatcher)
        {
            Content = new LoadingPage();

            dispatcher.BeginInvoke(() =>
            {
                var vm = new MainWindowViewModel(dispatcher);
                Content = new MainPage(vm);
            }, DispatcherPriority.ContextIdle);
        }

        [ObservableProperty]
        private Page content;
    }

    /// <summary>
    /// Interaction logic for AppWindow.xaml
    /// </summary>
    public partial class AppWindow : Window
    {
        public AppWindow()
        {
            DataContext = new AppWindowViewModel(Dispatcher);
            InitializeComponent();
        }
    }
}
