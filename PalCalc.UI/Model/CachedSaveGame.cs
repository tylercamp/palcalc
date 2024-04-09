using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    internal class CachedSaveGame
    {
        public DateTime LastModified { get; set; }
        public string FolderPath { get; set; }
        public string WorldName { get; set; }
        public string PlayerName { get; set; }
        public int PlayerLevel { get; set; }
        public List<PalInstance> OwnedPals { get; set; }

        public SaveGame UnderlyingSave => new SaveGame(FolderPath);

        public bool IsValid => UnderlyingSave.IsValid;

        public bool IsOutdated => LastModified != UnderlyingSave.LastModified;

        public static string IdentifierFor(SaveGame game)
        {
            var userFolderName = Path.GetFileName(Path.GetDirectoryName(game.BasePath));
            var saveName = game.FolderName;

            return $"{userFolderName}-{saveName}";
        }

        public static CachedSaveGame FromSaveGame(SaveGame game, PalDB db)
        {
            var meta = game.LevelMeta.ReadGameOptions();
            return new CachedSaveGame()
            {
                LastModified = game.LastModified,
                FolderPath = game.BasePath,
                OwnedPals = game.Level.ReadPalInstances(db),
                PlayerLevel = meta.PlayerLevel,
                PlayerName = meta.PlayerName,
                WorldName = meta.WorldName,
            };
        }

        public string ToJson(PalDB db) => JsonConvert.SerializeObject(this, new PalInstanceJsonConverter(db));

        public static CachedSaveGame FromJson(string json, PalDB db) => JsonConvert.DeserializeObject<CachedSaveGame>(json, new PalInstanceJsonConverter(db));

    }
}
