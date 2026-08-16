using AdonisUI.Controls;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;

namespace PalCalc.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string Version => "v1.19.1";
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
                .WriteTo.File(Logging.MessageFormat, $"{LogFolder}/log.txt", rollingInterval: RollingInterval.Day, levelSwitch: PCDebug.FileLogLevel)
#endif
                .CreateLogger();

            logger = Log.ForContext<App>();
            logger.Information($"Pal Calc version {Version}");

            PalDB.BeginLoadEmbedded();
            Task.Run(() =>
            {
                // start loading breeding DB early as well, reduces "Initializing" step when solver first runs
                var db = PalDB.LoadEmbedded();
                PalBreedingDB.BeginLoadEmbedded(db);
            });

            Translator.OnTranslationError += TranslationErrors.Add;
            Translator.Init();

            Dispatcher.BeginInvoke(() =>
            {
                if (LibOoz.IsMissingDependencies)
                {
                    var mb = new MessageBoxModel()
                    {
                        Text = LocalizationCodes.LC_LIBOOZ_VC_REDIST_MISSING.Bind().Value,
                        Buttons = [
                            new MessageBoxButtonModel(LocalizationCodes.LC_LIBOOZ_VC_REDIST_BTN_DOWNLOAD.Bind().Value, AdonisUI.Controls.MessageBoxResult.Yes),
                            new MessageBoxButtonModel(LocalizationCodes.LC_LIBOOZ_VC_REDIST_BTN_MORE_INFO.Bind().Value, AdonisUI.Controls.MessageBoxResult.Custom),
                            MessageBoxButtons.No()
                        ],
                    };
                    mb.SetDefaultButton(AdonisUI.Controls.MessageBoxResult.No);

                    switch (AdonisMessageBox.Show(mb))
                    {
                        case AdonisUI.Controls.MessageBoxResult.Yes:
                            Process.Start(new ProcessStartInfo() { FileName = "https://aka.ms/vc14/vc_redist.x64.exe", UseShellExecute = true });
                            break;

                        case AdonisUI.Controls.MessageBoxResult.Custom:
                            Process.Start(new ProcessStartInfo() { FileName = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170#latest-supported-redistributable-version", UseShellExecute = true });
                            break;
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SaveCustomizationsViewModel.FlushAll();
            base.OnExit(e);
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
                AdonisMessageBox.Show(message, caption: "");
            }
        }
    }
}
