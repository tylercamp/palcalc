using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using PalCalc.Model;
using PalCalc.UI2.Model;
using PalCalc.UI2.Model.Storage;
using PalCalc.UI2.View;
using PalCalc.UI2.ViewModel;
using PalCalc.UI2.ViewModels;
using Serilog;
using System;
using System.IO;

namespace PalCalc.UI2;

public partial class App : Application
{
    public static string Version => "v1.4.0";
    public static string RepositoryUrl => "https://github.com/tylercamp/palcalc/";

    private static ILogger logger;

    public static string LogFolder = "log";

    public App()
    {
#if RELEASE
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif

        if (!Directory.Exists(LogFolder)) Directory.CreateDirectory(LogFolder);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .PalCommon()
#if RELEASE
                .WriteTo.File(Logging.MessageFormat, $"{LogFolder}/log.txt", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
#endif
            .CreateLogger();

        logger = Log.ForContext<App>();
        logger.Information($"Pal Calc version {Version}");

        PalDB.BeginLoadEmbedded();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        logger.Fatal(e.ExceptionObject as Exception, "An unhandled error occurred");

        Serilog.Log.CloseAndFlush();
        var logZip = CrashSupport.PrepareSupportFile();
        // TODO
        //MessageBox.Show($"An unhandled error occurred.\n\nPlease find the generated ZIP file to send with any support questions:\n\n{logZip}");
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainVM()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
