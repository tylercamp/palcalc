using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.CSV;
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
using System.Windows.Threading;
using AdonisMessageBox = AdonisUI.Controls.MessageBox;

namespace PalCalc.UI.ViewModel
{
    internal partial class AppToolbarViewModel(Dispatcher dispatcher) : ObservableObject
    {
        private static ILogger logger = Log.ForContext<AppToolbarViewModel>();

        private static AppToolbarViewModel designerInstance;
        public static AppToolbarViewModel DesignerInstance => designerInstance ??= new(Dispatcher.CurrentDispatcher);

        public List<TranslationLocaleViewModel> Locales { get; } =
            Enum
                .GetValues<TranslationLocale>()
                .Select(l => new TranslationLocaleViewModel(l))
                .ToList();

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
    }
}
