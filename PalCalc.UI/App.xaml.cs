using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static ILogger logger;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var logFolder = "log";
            if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.PalCommon()
#if RELEASE
                .WriteTo.File(Logging.MessageFormat, $"{logFolder}/log.txt", rollingInterval: RollingInterval.Day)
#endif
                .CreateLogger();

            logger = Log.ForContext<App>();
            base.OnStartup(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Fatal(e.ExceptionObject as Exception, "An unhandled error occurred");
        }
    }
}
