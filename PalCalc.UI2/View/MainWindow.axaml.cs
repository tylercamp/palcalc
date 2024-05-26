using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.UI2.ViewModel;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.UserDataTasks;
using Windows.UI.Core;

namespace PalCalc.UI2.View;

internal partial class WindowVM : BaseVM
{
    public WindowVM()
    {
        displayedControl = new LoadingView();
        Task.Run(() =>
        {
            var result = new MainVM();

            Dispatcher.UIThread.Post(() => DisplayedControl = new MainView() { DataContext = result });
        });
    }

    private Control displayedControl;
    public Control DisplayedControl
    {
        get => displayedControl;
        set => SetProperty(ref displayedControl, value);
    }
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
