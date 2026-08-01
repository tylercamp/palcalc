using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButtonLabels = AdonisUI.Controls.MessageBoxButtonLabels;
using AdonisMessageBoxButtons = AdonisUI.Controls.MessageBoxButtons;
using AdonisMessageBoxModel = AdonisUI.Controls.MessageBoxModel;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI
{
    record class NewAppVersion(string Version, string Url);

    internal enum AppUpdateCheckStatus
    {
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    internal record class AppUpdateCheckResult(AppUpdateCheckStatus Status, NewAppVersion Version = null);

    static class AppUpdates
    {
        private static ILogger logger = Log.ForContext(typeof(AppUpdates));

        private static string VersionFromUrl(string url) => url.Split('/').Last();

        public static async Task<AppUpdateCheckResult> CheckForUpdates()
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
                        logger.Warning("expected FOUND status but got {code}, unable to determine latest version", response.StatusCode);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Warning(e, "error fetching latest version");
            }

            return new AppUpdateCheckResult(AppUpdateCheckStatus.Failed);
        }

        public static void PromptUpdateDownload(NewAppVersion version)
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
                Process.Start(new ProcessStartInfo { FileName = version.Url, UseShellExecute = true });
            }
            else if (res == AdonisMessageBoxResult.No)
            {
                AppSettings.Current.SkippedAppVersion = version.Version;
                Storage.SaveAppSettings(AppSettings.Current);
            }
        }
    }
}
