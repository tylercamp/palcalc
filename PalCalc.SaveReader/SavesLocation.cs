using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    public class SavesLocation
    {
        private static ILogger logger = Log.ForContext<SavesLocation>();

        public SavesLocation(string folderPath)
        {
            FolderPath = folderPath;
        }

        public string FolderPath { get; }
        public string FolderName => Path.GetFileName(FolderPath);

        public IEnumerable<SaveGame> AllSaveGames => Directory.EnumerateDirectories(FolderPath).Select(d => new SaveGame(d));
        public IEnumerable<SaveGame> ValidSaveGames => AllSaveGames.Where(g => g.IsValid);


        private static List<SavesLocation> FindAllForWindows()
        {
            var mainSavePath = Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%/Pal/Saved/SaveGames");
            logger.Information("checking for save game folders in {mainSavePath}", mainSavePath);
            // this file has multiple ID-like sub-folders, I assume there's one for each steam/etc. user, but
            // it also has a `UserOption.sav` which seems to store things like joystick sensitivity?

            if (Directory.Exists(mainSavePath))
            {
                return Directory.EnumerateDirectories(mainSavePath).Select(d => new SavesLocation(d)).ToList();
            }
            else
            {
                logger.Information("folder does not exist");
                return new List<SavesLocation>();
            }
        }

        public static List<SavesLocation> AllLocal
        {
            get
            {
                logger.Information("listing all local saves from known file locations");

                if (OperatingSystem.IsWindows()) return FindAllForWindows();
                else
                {
                    logger.Warning("unsupported platform");
                    return new List<SavesLocation>();
                }
            }
        }
    }
}
