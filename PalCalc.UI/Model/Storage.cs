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
    internal static class Storage
    {
        public static string CachePath => "cache";
        public static string SaveCachePath => $"{CachePath}/saves";

        private static bool didInit = false;
        private static void Init()
        {
            if (didInit) return;

            if (!Directory.Exists(CachePath)) Directory.CreateDirectory(CachePath);
            if (!Directory.Exists(SaveCachePath)) Directory.CreateDirectory(SaveCachePath);

            didInit = true;
        }

        public static CachedSaveGame LoadSave(SaveGame save, PalDB db)
        {
            Init();

            if (!save.IsValid) return null;

            var identifier = CachedSaveGame.IdentifierFor(save);
            var path = $"{SaveCachePath}/{identifier}.json";
            if (File.Exists(path))
            {
                var res = CachedSaveGame.FromJson(File.ReadAllText(path), db);
                if (!res.IsValid)
                {
                    File.Delete(path);
                    return null;
                }

                if (res.IsOutdated)
                {
                    File.Delete(path);
                    return LoadSave(save, db);
                }

                return res;
            }
            else
            {
                var res = CachedSaveGame.FromSaveGame(save, db);
                File.WriteAllText(path, res.ToJson(db));
                return res;
            }
        }
    }
}
