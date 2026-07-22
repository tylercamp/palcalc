using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using PalCalc.UI.Localization;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxModel = AdonisUI.Controls.MessageBoxModel;
using AdonisMessageBoxButtons = AdonisUI.Controls.MessageBoxButtons;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;
using AdonisMessageBoxButtonLabels = AdonisUI.Controls.MessageBoxButtonLabels;
using PalCalc.UI.Model;

namespace PalCalc.UI.ViewModel
{
    // TODO - Re-review and refactor

    record class NewAppVersion(string Version, string Url);

    internal enum AppUpdateCheckStatus
    {
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    internal record class AppUpdateCheckResult(AppUpdateCheckStatus Status, NewAppVersion Version = null);

    class AppUpdatesViewModel
    {
        private static ILogger logger = Log.ForContext<AppUpdatesViewModel>();

        private string VersionFromUrl(string url) => url.Split('/').Last();

        public async Task<AppUpdateCheckResult> FetchNewUpdateUrl()
        {
            try
            {
                var latestReleaseUrl = $"{App.RepositoryUrl}/releases/latest";
                using (var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false }))
                using (var response = await client.GetAsync(latestReleaseUrl))
                {
                    if (response.StatusCode == HttpStatusCode.Found)
                    {
                        var location = response.Headers?.Location;
                        if (location != null)
                        {
                            var latestVersion = VersionFromUrl(location.AbsoluteUri);
                            if (latestVersion != App.Version)
                            {
                                return new AppUpdateCheckResult(AppUpdateCheckStatus.UpdateAvailable, new NewAppVersion(latestVersion, location.AbsoluteUri));
                            }

                            return new AppUpdateCheckResult(AppUpdateCheckStatus.UpToDate);
                        }
                        else
                        {
                            logger.Warning("releases response did not include redirect URL, unable to determine latest version");
                        }
                    }
                    else
                    {
                        logger.Warning("did not receive FOUND status code, unable to determine latest version");
                    }
                }
            }
            catch (Exception e)
            {
                logger.Warning(e, "error fetching latest version");
            }

            return new AppUpdateCheckResult(AppUpdateCheckStatus.Failed);
        }

        public void OpenUpdateUrl(string url) =>
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

        public void PromptUpdateDownload(NewAppVersion version)
        {
            var res = AdonisMessageBox.Show(App.Current.MainWindow, new AdonisMessageBoxModel()
            {
                Caption = LocalizationCodes.LC_UPDATE_AVAILABLE.Bind().Value,
                Text = LocalizationCodes.LC_UPDATES_CHECK_BODY.Bind(version.Version).Value,
                Buttons = [
                    AdonisMessageBoxButtons.Yes(),
                    AdonisMessageBoxButtons.Cancel(AdonisMessageBoxButtonLabels.No),
                    AdonisMessageBoxButtons.No(LocalizationCodes.LC_UPDATES_CHECK_OPTION_SKIP.Bind().Value)
                ]
            });

            if (res == AdonisMessageBoxResult.Yes)
            {
                OpenUpdateUrl(version.Url);
            }
            else if (res == AdonisMessageBoxResult.No)
            {
                AppSettings.Current.SkippedAppVersion = version.Version;
                Storage.SaveAppSettings(AppSettings.Current);
            }
        }
    }
}
