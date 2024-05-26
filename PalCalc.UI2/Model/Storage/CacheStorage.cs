using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model.Storage
{
    internal class CacheStorage : ICalcStorage
    {
        public CacheStorage(string basePath) : base(basePath)
        {
        }

        private string SaveDetailsSubpath(ISaveGame save) => EnsuredPathOfFile($"save-details-{save.Identifier()}.json");

        public void StoreSaveDetails(SaveGameDetails details)
        {
            var outputPath = SaveDetailsSubpath(details.UnderlyingSave);
            var serialized = JsonConvert.SerializeObject(details, new PalInstanceJsonConverter(PalDB.LoadEmbedded()));
            File.WriteAllText(outputPath, serialized);
        }

        public SaveGameDetails? LoadSaveDetails(ISaveGame save)
        {
            var path = SaveDetailsSubpath(save);
            if (File.Exists(SaveDetailsSubpath(save)))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<SaveGameDetails>(json, new PalInstanceJsonConverter(PalDB.LoadEmbedded()));
            }
            else
            {
                return null;
            }
        }

        public void AttachSupportFiles(ZipArchive archive, string targetName, ISaveGame requestedSave)
        {
            var path = SaveDetailsSubpath(requestedSave);
            if (File.Exists(path)) archive.CreateEntryFromFile(targetName, path);
        }
    }
}
