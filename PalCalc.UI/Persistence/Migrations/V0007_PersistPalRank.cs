using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0007_PersistPalRank : StorageMigration
    {
        public V0007_PersistPalRank() : base(6, 7) { }

        public override void Apply(StorageMigrationContext context)
        {
            foreach (var path in Directory.EnumerateFiles(context.DataPath, "custom-containers.json", SearchOption.AllDirectories))
            {
                var customizations = JObject.Parse(File.ReadAllText(path));
                foreach (var container in customizations["CustomContainers"] as JArray ?? [])
                    foreach (var instance in container?["Contents"] as JArray ?? [])
                        if (instance is JObject palInstance)
                            palInstance["Rank"] ??= 1;
                StorageFile.WriteAtomic(path, customizations.ToString(Formatting.None), backup: true);
            }

            foreach (var targetsPath in Directory.EnumerateDirectories(context.DataPath, "targets", SearchOption.AllDirectories))
            {
                foreach (var path in Directory.EnumerateFiles(targetsPath, "*.json"))
                {
                    var target = JObject.Parse(File.ReadAllText(path));
                    if (target["CurrentResults"] is JObject results)
                    {
                        foreach (var result in results["Results"] as JArray ?? [])
                            if (result?["PalReference"] is JObject reference)
                                AddRank(reference);
                    }
                    StorageFile.WriteAtomic(path, target.ToString(Formatting.None), backup: true);
                }
            }
        }

        private static void AddRank(JObject reference)
        {
            if (reference["Instance"] is JObject instance)
                instance["Rank"] ??= 1;

            foreach (var property in new[] { "Parent1", "Parent2", "Male", "Female", "Input" })
                if (reference[property] is JObject child)
                    AddRank(child);
        }
    }
}
