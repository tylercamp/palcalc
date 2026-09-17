using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0006_PersistLevelRequirements : StorageMigration
    {
        public V0006_PersistLevelRequirements() : base(5, 6) { }

        public override void Apply(StorageMigrationContext context)
        {
            foreach (var targetsPath in Directory.EnumerateDirectories(context.DataPath, "targets", SearchOption.AllDirectories))
            {
                foreach (var path in Directory.EnumerateFiles(targetsPath, "*.json"))
                {
                    var target = JObject.Parse(File.ReadAllText(path));
                    if (target["CurrentResults"] is JObject results)
                    {
                        foreach (var result in results["Results"] as JArray ?? [])
                            if (result?["PalReference"] is JObject reference)
                                AddLevelRequirements(reference);
                    }
                    StorageFile.WriteAtomic(path, target.ToString(Formatting.None), backup: true);
                }
            }
        }

        private static void AddLevelRequirements(JObject reference)
        {
            reference["LevelRequirements"] ??= JValue.CreateNull();
            foreach (var property in new[] { "Parent1", "Parent2", "Male", "Female", "Input" })
                if (reference[property] is JObject child)
                    AddLevelRequirements(child);
        }
    }
}
