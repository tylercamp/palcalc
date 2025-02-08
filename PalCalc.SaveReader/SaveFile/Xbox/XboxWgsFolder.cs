using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Bluetooth;
using Windows.Management.Deployment;
using Windows.Storage;

namespace PalCalc.SaveReader.SaveFile.Xbox
{
    public class XboxWgsEntry
    {
        /// <summary>
        /// The path to the data file on disk
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The proper, readable name of the file according to the Xbox save container index
        /// </summary>
        public string FileName { get; set; }
    }

    public class XboxWgsFolder
    {
        public string UserBasePath { get; }
        public string ContainerIndexPath { get; }

        public XboxFolderMonitor Monitor { get; }

        private List<XboxWgsEntry> cachedEntries;
        public IEnumerable<XboxWgsEntry> Entries => cachedEntries ??= PalworldWgsEntries.ToList();

        public XboxWgsFolder(string userBasePath)
        {
            UserBasePath = userBasePath;
            ContainerIndexPath = Path.Combine(UserBasePath, "containers.index");

            Monitor = new XboxFolderMonitor(UserBasePath);
            Monitor.Updated += Monitor_Updated;
        }

        private void Monitor_Updated()
        {
            cachedEntries = null;
        }

        private IEnumerable<XboxWgsEntry> PalworldWgsEntries
        {
            get
            {
                var dataContainer = Container.TryParse(ContainerIndexPath);
                if (dataContainer == null) yield break;

                // save files that are part of a single save are grouped by the first part of their name

                foreach (var saveFileFolder in dataContainer.Folders.Where(f => f.Name.Count(c => c == '-') != 0))
                {
                    var saveGameFiles = ContainerFile.TryParse(saveFileFolder);
                    foreach (var saveFile in saveGameFiles.Where(f => File.Exists(f.Path)))
                    {
                        // all of the files are stored in their own folders, where the "real" file name is always just "Data"
                        if (saveFile.Name != "Data") continue;

                        if (File.Exists(saveFile.Path))
                            yield return new XboxWgsEntry() { FilePath = saveFile.Path, FileName = saveFileFolder.Name };
                    }
                }
            }
        }

        public static IEnumerable<XboxWgsFolder> FindAll()
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

            if (palworldPkg == null) yield break;

            foreach (var userFolder in Directory.EnumerateDirectories(Path.Combine(pkgDir, "SystemAppData\\wgs")))
                yield return new XboxWgsFolder(userFolder);
        }
    }
}
