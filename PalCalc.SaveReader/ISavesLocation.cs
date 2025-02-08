using PalCalc.Model;
using PalCalc.SaveReader.SaveFile;
using PalCalc.SaveReader.SaveFile.Xbox;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Email.DataProvider;
using Windows.Management.Deployment;
using Windows.Security.Cryptography.Core;
using Windows.Storage;

namespace PalCalc.SaveReader
{
    public interface ISavesLocation
    {
        public string FolderPath { get; }
        public string FolderName => FolderPath == null ? "None" : Path.GetFileName(FolderPath);

        public IEnumerable<ISaveGame> AllSaveGames { get; }
        public IEnumerable<ISaveGame> ValidSaveGames => AllSaveGames.Where(g => g.IsValid);
    }

    public class DirectSavesLocation : ISavesLocation
    {
        private static ILogger logger = Log.ForContext<DirectSavesLocation>();

        public DirectSavesLocation(string folderPath)
        {
            FolderPath = folderPath;
        }

        public string FolderPath { get; }
        public string FolderName => Path.GetFileName(FolderPath);

        public IEnumerable<ISaveGame> AllSaveGames => Directory.EnumerateDirectories(FolderPath).Select(d => new StandardSaveGame(d));
        public IEnumerable<ISaveGame> ValidSaveGames => AllSaveGames.Where(g => g.IsValid);


        private static List<DirectSavesLocation> FindAllForWindows()
        {
            var mainSavePath = Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%/Pal/Saved/SaveGames");
            logger.Information("checking for save game folders in {mainSavePath}", mainSavePath);
            // this file has multiple ID-like sub-folders, I assume there's one for each steam/etc. user, but
            // it also has a `UserOption.sav` which seems to store things like joystick sensitivity?

            if (Directory.Exists(mainSavePath))
            {
                return Directory.EnumerateDirectories(mainSavePath).Select(d => new DirectSavesLocation(d)).ToList();
            }
            else
            {
                logger.Information("folder does not exist");
                return new List<DirectSavesLocation>();
            }
        }

        public static List<DirectSavesLocation> AllLocal
        {
            get
            {
                logger.Information("listing all local saves from known file locations");

                if (OperatingSystem.IsWindows()) return FindAllForWindows();
                else
                {
                    logger.Warning("unsupported platform");
                    return new List<DirectSavesLocation>();
                }
            }
        }
    }

    // possible thanks to https://github.com/Tom60chat/Xbox-Live-Save-Exporter/
    // (where that was possible thanks to "HunterStanton" and "snoozbuster", see that repo's README)
    public class XboxSavesLocation : ISavesLocation
    {
        private static ILogger logger = Log.ForContext<XboxSavesLocation>();

        public static List<XboxSavesLocation> FindAll()
        {
            var result = new List<XboxSavesLocation>();

            foreach (var wgsFolder in XboxWgsFolder.FindAll())
            {
                try
                {
                    var allSaveFiles = wgsFolder.Entries.ToList();

                    var saveGames = allSaveFiles
                        .GroupBy(xsf => xsf.FileName.Split('-')[0])
                        .Select(g =>
                        {
                            var saveId = g.Key;
                            return new XboxSaveGame(wgsFolder, g.Key);
                        })
                        .ToList();

                    if (saveGames.Count == 0) continue;

                    result.Add(new XboxSavesLocation(wgsFolder.UserBasePath, saveGames));
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "error reading Xbox save data from user folder {path}", wgsFolder.UserBasePath);
                }
            }

            return result;
        }

        private XboxSavesLocation(string userFolderPath, List<XboxSaveGame> saves)
        {
            try
            {
                FolderPath = userFolderPath;
                AllSaveGames = saves;
            }
            catch
            {
                AllSaveGames = new List<ISaveGame>();
            }
        }

        public XboxSavesLocation()
        {
            AllSaveGames = new List<ISaveGame>();
            FolderPath = null;
        }

        public string FolderPath { get; }

        public IEnumerable<ISaveGame> AllSaveGames { get; }
    }
}
