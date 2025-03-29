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
            // (throws DirectoryNotFoundException if path is missing)
            Directory.Exists(basePath)
                ? Directory
                    .EnumerateFiles(basePath)
                    .Where(p => matcher(Path.GetFileName(p)))
                    .OrderBy(Path.GetFileNameWithoutExtension)
                : [];
    }

    public class XboxFileSource(XboxWgsFolder saveFolder, string saveId, Func<string, bool> matcher) : IFileSource
    {
        public IEnumerable<XboxWgsEntry> XboxContent =>
            saveFolder.Entries
                .Where(e => e.FileName.StartsWith($"{saveId}-"))
                .Where(e => matcher(e.FileName.Replace($"{saveId}-", "")))
                .OrderBy(e => e.FileName.Contains('-') ? e.FileName.Split("-").Last() : "");

        public IEnumerable<string> Content => XboxContent.Select(e => e.FilePath).ToList();
    }
}
