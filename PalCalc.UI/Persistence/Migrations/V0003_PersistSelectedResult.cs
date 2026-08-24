using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.UI.Persistence;
using System.IO;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0003_PersistSelectedResult : StorageMigration
    {
        public V0003_PersistSelectedResult() : base(2, 3) { }

        public override void Apply(StorageMigrationContext context)
        {
            foreach (var targetsPath in Directory.EnumerateDirectories(context.DataPath, "targets", SearchOption.AllDirectories))
            {
                foreach (var path in Directory.EnumerateFiles(targetsPath, "*.json"))
                {
                    var root = JObject.Parse(File.ReadAllText(path));
                    var results = root["CurrentResults"] as JObject;
                    if (results == null || results["SelectedResultIndex"]?.Type == JTokenType.Integer)
                        continue;

                    results["SelectedResultIndex"] = -1;
                    StorageFile.WriteAtomic(path, root.ToString(Formatting.None), backup: true);
                }
            }
        }
    }
}
