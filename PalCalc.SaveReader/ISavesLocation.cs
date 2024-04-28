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
using Windows.Management.Deployment;
using Windows.Storage;

namespace PalCalc.SaveReader
{
    public interface ISavesLocation
    {
        public string FolderPath { get; }
        public string FolderName => Path.GetFileName(FolderPath);

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

        private class XboxSaveFile
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
        }

        public static List<XboxSavesLocation> FindAll()
        {
            var LocalPackagesPath = Path.Combine(UserDataPaths.GetDefault().LocalAppData, "Packages");
            var pm = new PackageManager();

            string pkgDir = null;
            Package palworldPkg = null;

            // Logo = {file:///C:/Program Files/WindowsApps/Resources/PocketpairInc.Palworld_0.0.51499.0_x64__ad4psfrxyesvt/Resources/StoreLogo.png}

            foreach (var packageDir in Directory.EnumerateDirectories(LocalPackagesPath))
            {
                if (!Directory.Exists(Path.Join(packageDir, "SystemAppData\\wgs"))) continue;

                // note: Xbox-Live-Save-Exporter checks `package.DisplayName`, which is a more accurate way of checking the app name,
                //       but the `.DisplayName` call seems to take forever. The directory for palworld data should also contain the
                //       "Palworld" string, so this should be fine
                if (!Path.GetFileName(packageDir).Contains("Palworld", StringComparison.InvariantCultureIgnoreCase)) continue;

                var package = pm.FindPackagesForUser(null, Path.GetFileName(packageDir))?.FirstOrDefault();

                palworldPkg = package;
                pkgDir = packageDir;
                break;
            }

            if (palworldPkg == null) return new List<XboxSavesLocation>();

            var result = new List<XboxSavesLocation>();
            foreach (var userFolder in Directory.EnumerateDirectories(Path.Combine(pkgDir, "SystemAppData\\wgs")))
            {
                try
                {
                    var dataContainer = Container.TryParse(Path.Combine(userFolder, "containers.index"));
                    if (dataContainer == null) continue;

                    // save files that are part of a single save are grouped by the first part of their name
                    var collectedSaveFiles = new List<XboxSaveFile>();
                    foreach (var saveFileFolder in dataContainer.Folders)
                    {
                        // folder names are all `SAVEID-FileName`
                        if (saveFileFolder.Name.Count(c => c == '-') != 1) continue;

                        var saveGameFiles = ContainerFile.TryParse(saveFileFolder);
                        foreach (var saveFile in saveGameFiles)
                        {
                            // all of the files are stored in their own folders, where the "real" file name is always just "Data"
                            if (saveFile.Name != "Data") continue;

                            collectedSaveFiles.Add(new XboxSaveFile() { FilePath = saveFile.Path, FileName = saveFileFolder.Name });
                        }
                    }

                    var saveGames = collectedSaveFiles
                        .GroupBy(xsf => xsf.FileName.Split('-')[0])
                        .Select(g =>
                        {
                            LevelSaveFile level = null;
                            LevelMetaSaveFile levelMeta = null;
                            LocalDataSaveFile localData = null;
                            WorldOptionSaveFile worldOption = null;

                            foreach (var xsf in g)
                            {
                                switch (xsf.FileName.Split('-')[1])
                                {
                                    case "Level":
                                        level = new LevelSaveFile(xsf.FilePath);
                                        break;

                                    case "LevelMeta":
                                        levelMeta = new LevelMetaSaveFile(xsf.FilePath);
                                        break;

                                    case "LocalData":
                                        localData = new LocalDataSaveFile(xsf.FilePath);
                                        break;

                                    case "WorldOption":
                                        worldOption = new WorldOptionSaveFile(xsf.FilePath);
                                        break;
                                }
                            }

                            return new XboxSaveGame(userFolder, g.Key, level, levelMeta, localData, worldOption);
                        })
                        .ToList();

                    result.Add(new XboxSavesLocation(userFolder, saveGames));
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "error reading Xbox save data from user folder {path}", userFolder);
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

        public string FolderPath { get; }

        public IEnumerable<ISaveGame> AllSaveGames { get; }
    }
}
