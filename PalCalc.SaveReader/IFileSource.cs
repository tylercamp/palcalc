using PalCalc.SaveReader.SaveFile.Xbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    // TODO - make CompressedSAV accept these so we can more neatly handle file-change race conditions
    public interface IFileSource
    {
        IEnumerable<string> Content { get; }
    }

    public class SingleFileSource(string exactPath) : IFileSource
    {
        public IEnumerable<string> Content => [exactPath];
    }

    public class FilteredFileSource(string basePath, Func<string, bool> matcher) : IFileSource
    {
        public IEnumerable<string> Content =>
            Directory
                .EnumerateFiles(basePath)
                .Where(p => matcher(Path.GetFileName(p)))
                .OrderBy(Path.GetFileNameWithoutExtension);
    }

    public class XboxFileSource(string containersIndexPath, string saveId, Func<string, bool> matcher) : IFileSource
    {
        protected class SaveEntry
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
        }

        private IEnumerable<SaveEntry> AllEntries
        {
            get
            {
                var dataContainer = Container.TryParse(containersIndexPath);

                // save files that are part of a single save are grouped by the first part of their name

                foreach (var saveFileFolder in dataContainer.Folders.Where(f => f.Name.Count(c => c == '-') != 0))
                {
                    var saveGameFiles = ContainerFile.TryParse(saveFileFolder);
                    foreach (var saveFile in saveGameFiles.Where(f => File.Exists(f.Path)))
                    {
                        // all of the files are stored in their own folders, where the "real" file name is always just "Data"
                        if (saveFile.Name != "Data") continue;

                        if (!saveFileFolder.Name.StartsWith($"{saveId}-")) continue;

                        if (File.Exists(saveFile.Path))
                            yield return new SaveEntry() { FilePath = saveFile.Path, FileName = saveFileFolder.Name.Replace($"{saveId}-", "") };
                    }
                }
            }
        }

        public IEnumerable<string> Content =>
            AllEntries
                .Where(e => matcher(e.FileName))
                .OrderBy(e => e.FileName.Contains('-') ? e.FileName.Split("-").Last() : "")
                .Select(e => e.FilePath);
    }
}
