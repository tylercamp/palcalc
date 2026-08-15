using AdonisUI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.CSV;
using PalCalc.UI.Model.Service;
using PalCalc.UI.View;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.SaveSelection;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AdonisMessageBox = AdonisUI.Controls.MessageBox;

namespace PalCalc.UI.ViewModel
{
    internal partial class AppToolbarViewModel(Dispatcher dispatcher) : ObservableObject
    {
        private static readonly Uri palCalcDarkColorScheme = new("pack://application:,,,/PalCalc.UI;component/Themes/PalCalcDark.xaml", UriKind.Absolute);
        private static readonly Uri palCalcLightColorScheme = new("pack://application:,,,/PalCalc.UI;component/Themes/PalCalcLight.xaml", UriKind.Absolute);

        private static ILogger logger = Log.ForContext<AppToolbarViewModel>();

        private static AppToolbarViewModel designerInstance;
        public static AppToolbarViewModel DesignerInstance => designerInstance ??= new(Dispatcher.CurrentDispatcher);

        private Uri currentPalCalcColorScheme = palCalcDarkColorScheme;

        public List<TranslationLocaleViewModel> Locales { get; } =
            Enum
                .GetValues<TranslationLocale>()
                .Select(l => new TranslationLocaleViewModel(l))
                .ToList();

        public bool IsDebugLoggingEnabled
        {
            get => PCDebug.FileLogLevel.MinimumLevel == Serilog.Events.LogEventLevel.Debug;
            set => PCDebug.FileLogLevel.MinimumLevel = value ? Serilog.Events.LogEventLevel.Debug : PCDebug.DefaultFileLogLevel;
        }

        [RelayCommand]
        private void ExportCrashLog()
        {
            var sfd = new SaveFileDialog();
            sfd.FileName = "CRASHLOG.zip";
            sfd.Filter = "ZIP | *.zip";
            sfd.AddExtension = true;
            sfd.DefaultExt = "zip";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    CrashSupport.PrepareSupportFile(sfd.FileName);
                }
                catch (Exception e)
                {
                    logger.Warning(e, "unexpected error when attempting to create crashlog file");
                    AdonisMessageBox.Show(LocalizationCodes.LC_CRASHLOG_FAILED.Bind().Value, caption: "");
                }
            }
        }

        [RelayCommand]
        private void OpenAboutWindow()
        {
            var window = new AboutWindow();
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
        }

        [RelayCommand]
        private void ResetUiLayout()
        {
            UILayoutStore.Reset();
            AdonisMessageBox.Show(
                App.Current.MainWindow,
                LocalizationCodes.LC_UI_LAYOUT_RESET_NOTICE.Bind().Value
            );
        }

        [RelayCommand]
        private void ForceCheckForUpdates()
        {
            Task.Run(async () =>
            {
                var result = await AppUpdates.CheckForUpdates();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // don't need to run this synchronously
                dispatcher.BeginInvoke(() =>
                {
                    if (result.Status == AppUpdateCheckStatus.Failed)
                    {
                        AdonisMessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_UPDATES_CHECK_RESULT_FAILED.Bind().Value);
                        return;
                    }

                    if (result.Status == AppUpdateCheckStatus.UpToDate)
                    {
                        AdonisMessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_UPDATES_CHECK_RESULT_ON_LATEST.Bind().Value);
                        return;
                    }

                    AppUpdates.PromptUpdateDownload(result.Version);
                }, DispatcherPriority.ContextIdle);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            });
        }

        [RelayCommand]
        private void UseDarkTheme()
        {
            SetTheme(ResourceLocator.DarkColorScheme, palCalcDarkColorScheme);
        }

        [RelayCommand]
        private void UseLightTheme()
        {
            SetTheme(ResourceLocator.LightColorScheme, palCalcLightColorScheme);
        }

        private void SetTheme(Uri adonisColorScheme, Uri palCalcColorScheme)
        {
            ResourceLocator.SetColorScheme(Application.Current.Resources, adonisColorScheme);
            ResourceLocator.SetColorScheme(Application.Current.Resources, palCalcColorScheme, currentPalCalcColorScheme);
            currentPalCalcColorScheme = palCalcColorScheme;
        }
    }
}
