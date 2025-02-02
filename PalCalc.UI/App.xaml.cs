using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
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
        public static string Version => "v1.11.0";
        public static string RepositoryUrl => "https://github.com/tylercamp/palcalc/";

        private static ILogger logger;

        public static string LogFolder = "log";

        public static List<ITranslationError> TranslationErrors { get; } = new List<ITranslationError>();

        public static Window ActiveWindow => Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive) ?? Current.MainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
#if RELEASE
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif

            Storage.Init();

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

            Translator.OnTranslationError += TranslationErrors.Add;
            Translator.Init();

            base.OnStartup(e);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Fatal(e.ExceptionObject as Exception, "An unhandled error occurred");

            Serilog.Log.CloseAndFlush();
            var logZip = CrashSupport.PrepareSupportFile();

            var message = $"An unhandled error occurred.\n\nPlease find the generated ZIP file to send with any support questions:\n\n{logZip}";

            try
            {
                message = LocalizationCodes.LC_ERROR_HARD_CRASH.Bind(new { CrashlogPath = logZip }).Value;
            }
            finally
            {
                MessageBox.Show(message);
            }
        }
    }
}
