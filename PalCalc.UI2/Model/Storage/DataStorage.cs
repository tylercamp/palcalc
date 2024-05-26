using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model.Storage
{
    internal class DataStorage : ICalcStorage
    {
        public DataStorage(string relativeBasePath) : base(relativeBasePath)
        {
        }

        public void AttachSupportFiles(ZipArchive archive)
        {
            if (!Directory.Exists(RelativeBasePath)) return;

            foreach (var f in Directory.EnumerateFiles(RelativeBasePath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    archive.CreateEntryFromFile(f, f.Replace(Path.GetFullPath(RelativeBasePath), RelativeBasePath).TrimStart('/', '\\'));
                }
                catch { }
            }
        }
    }
}
