using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI2.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model.Storage
{
    internal abstract class ICalcStorage
    {
        protected string RelativeBasePath { get; }
        protected string PathOf(string path) => Path.Join(RelativeBasePath, path);
        protected string EnsuredPathOfFile(string path)
        {
            var directory = Path.GetDirectoryName(path)!;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return path;
        }

        public ICalcStorage(string relativeBasePath)
        {
            RelativeBasePath = relativeBasePath;
        }
    }

    internal static class StorageManager
    {
        public static readonly AppStorage App = new AppStorage("data");
        public static readonly CacheStorage Cache = new CacheStorage("cache");
        public static readonly DataStorage Data = new DataStorage("data");
    }
}
