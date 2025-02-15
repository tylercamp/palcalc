using AdonisUI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.UI.View;
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
            var loadingPage = new LoadingPage();
            Content = loadingPage;

            Task.Run(() =>
            {
                try
                {
                    var loadingVm = new LoadingPageViewModel();
                    var vm = new MainWindowViewModel(dispatcher, progress =>
                    {
                        dispatcher.BeginInvoke(() =>
                        {
                            loadingVm.ProgressPercent = progress.ProgressPercent;

                            if (loadingPage.DataContext == null)
                                loadingPage.DataContext = loadingVm;
                        });
                    });

                    dispatcher.BeginInvoke(() => Content = new MainPage(vm), DispatcherPriority.ContextIdle);
                }
                catch (Exception e)
                {
                    // (exceptions in Tasks are handled differently - re-send exceptions on UI Dispatcher so it gets handled like a normal error)
                    dispatcher.BeginInvoke(() =>
                    {
                        throw new Exception("An error occurred while loading the main window data", e);
                    });
                }
            });
        }

        [ObservableProperty]
        private Page content;
    }

    /// <summary>
    /// Interaction logic for AppWindow.xaml
    /// </summary>
    public partial class AppWindow : AdonisWindow
    {
        public AppWindow()
        {
            DataContext = new AppWindowViewModel(Dispatcher);
            InitializeComponent();

            // (`Content` inherits the DataContext of the window, but we don't want `LoadingPage` to inherit that)
            (Content as LoadingPage).DataContext = null;

#if DEBUG
            if (App.TranslationErrors.Count > 0)
            {
                var debugWindow = new TranslationDebugWindow();
                debugWindow.DataContext = new TranslationDebugViewModel(App.TranslationErrors);
                debugWindow.Show();
            }
#endif
        }
    }
}
