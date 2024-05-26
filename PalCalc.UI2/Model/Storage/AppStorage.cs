using Newtonsoft.Json;
using PalCalc.UI2.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model.Storage
{
    internal class AppStorage : ICalcStorage
    {
        private static ILogger logger = Log.ForContext<AppStorage>();

        public AppStorage(string basePath) : base(basePath)
        {
        }

        private string? appSettingsPath;
        private string AppSettingsPath => appSettingsPath ??= EnsuredPathOfFile("settings.json");

        public AppSettings LoadSettings()
        {
            if (File.Exists(AppSettingsPath))
            {
                try
                {
                    return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(AppSettingsPath))!;
                }
                catch (Exception e)
                {
                    logger.Warning(e, "error when loading app settings, resetting");
                    File.Delete(AppSettingsPath);

                    return new AppSettings();
                }
            }
            else
            {
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings) => File.WriteAllText(AppSettingsPath, JsonConvert.SerializeObject(settings));
    }
}
