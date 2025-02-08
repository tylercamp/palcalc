using PalCalc.SaveReader.SaveFile.Xbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    public interface IFileSource
    {
        IEnumerable<string> Content { get; }
    }

    public class SingleFileSource(string exactPath) : IFileSource
    {
        public IEnumerable<string> Content => [exactPath];
    }

    public class MultiFileSource(IEnumerable<string> paths) : IFileSource
    {
        public IEnumerable<string> Content => paths;
    }

    public class FilteredFileSource(string basePath, Func<string, bool> matcher) : IFileSource
    {
        public IEnumerable<string> Content =>
            Directory
                .EnumerateFiles(basePath)
                .Where(p => matcher(Path.GetFileName(p)))
                .OrderBy(Path.GetFileNameWithoutExtension);
    }

    public class XboxFileSource : IFileSource
    {
        XboxSaveMonitor saveMonitor;
        Func<string, bool> matcher;
        List<string> cachedResults;

        public XboxFileSource(XboxSaveMonitor saveMonitor, Func<string, bool> matcher)
        {
            this.saveMonitor = saveMonitor;
            this.matcher = matcher;

            saveMonitor.Updated += SaveMonitor_Updated;

            this.cachedResults = null;
        }

        private void SaveMonitor_Updated()
        {
            cachedResults = null;
        }

        protected class SaveEntry
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
        }

        // TODO - cache results and refresh when save file changes
        private IEnumerable<SaveEntry> AllEntries
        {
            get
            {
                var dataContainer = Container.TryParse(saveMonitor.ContainerIndexPath);

                // save files that are part of a single save are grouped by the first part of their name

                foreach (var saveFileFolder in dataContainer.Folders.Where(f => f.Name.Count(c => c == '-') != 0))
                {
                    var saveGameFiles = ContainerFile.TryParse(saveFileFolder);
                    foreach (var saveFile in saveGameFiles.Where(f => File.Exists(f.Path)))
                    {
                        // all of the files are stored in their own folders, where the "real" file name is always just "Data"
                        if (saveFile.Name != "Data") continue;

                        if (!saveFileFolder.Name.StartsWith($"{saveMonitor.SaveId}-")) continue;

                        if (File.Exists(saveFile.Path))
                            yield return new SaveEntry() { FilePath = saveFile.Path, FileName = saveFileFolder.Name.Replace($"{saveMonitor.SaveId}-", "") };
                    }
                }
            }
        }

        public IEnumerable<string> Content =>
            cachedResults ??= AllEntries
                .Where(e => matcher(e.FileName))
                .OrderBy(e => e.FileName.Contains('-') ? e.FileName.Split("-").Last() : "")
                .Select(e => e.FilePath).ToList();
    }
}
