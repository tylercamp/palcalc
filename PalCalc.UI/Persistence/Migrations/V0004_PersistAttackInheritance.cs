using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.UI.Persistence;
using System.IO;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0004_PersistAttackInheritance : StorageMigration
    {
        public V0004_PersistAttackInheritance() : base(3, 4) { }

        public override void Apply(StorageMigrationContext context)
        {
            var settingsPath = Path.Combine(context.DataPath, "settings.json");
            if (File.Exists(settingsPath))
            {
                var settings = JObject.Parse(File.ReadAllText(settingsPath));
                if (settings["SolverSettings"] is JObject solverSettings && solverSettings["MaxSpecialCakes"] == null)
                    solverSettings["MaxSpecialCakes"] = 0;
                StorageFile.WriteAtomic(settingsPath, settings.ToString(Formatting.None), backup: true);
            }

            foreach (var targetsPath in Directory.EnumerateDirectories(context.DataPath, "targets", SearchOption.AllDirectories))
            {
                foreach (var path in Directory.EnumerateFiles(targetsPath, "*.json"))
                {
                    var target = JObject.Parse(File.ReadAllText(path));
                    target["RequiredAttackInternalNames"] ??= new JArray();

                    if (target["CurrentResults"] is JObject results)
                    {
                        if (results["SolverSettings"] is JObject solverSettings && solverSettings["MaxSpecialCakes"] == null)
                            solverSettings["MaxSpecialCakes"] = 0;
                        foreach (var result in results["Results"] as JArray ?? [])
                        {
                            if (result?["PalReference"] is JObject reference)
                                AddReferenceFields(reference);
                        }
                    }

                    StorageFile.WriteAtomic(path, target.ToString(Formatting.None), backup: true);
                }
            }
        }

        private static void AddReferenceFields(JObject reference)
        {
            if (reference["AvgRequiredBreedings"] == null)
                reference["AvgRequiredBreedings"] = JValue.CreateNull();
            if (reference["MaterializedAttackInheritance"] == null)
                reference["MaterializedAttackInheritance"] = JValue.CreateNull();

            foreach (var property in new[] { "Parent1", "Parent2", "Male", "Female", "Input" })
            {
                if (reference[property] is JObject child)
                    AddReferenceFields(child);
            }
        }
    }
}
