using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0007_PersistSpecialCakePassivePolicy : StorageMigration
    {
        public V0007_PersistSpecialCakePassivePolicy() : base(6, 7) { }

        public override void Apply(StorageMigrationContext context)
        {
            var settingsPath = Path.Combine(context.DataPath, "settings.json");
            if (File.Exists(settingsPath))
            {
                var settings = JObject.Parse(File.ReadAllText(settingsPath));
                if (settings["SolverSettings"] is JObject solverSettings)
                    solverSettings["AllowSpecialCakesForPassives"] ??= false;
                StorageFile.WriteAtomic(settingsPath, settings.ToString(Formatting.None), backup: true);
            }

            foreach (var targetsPath in Directory.EnumerateDirectories(context.DataPath, "targets", SearchOption.AllDirectories))
            {
                foreach (var path in Directory.EnumerateFiles(targetsPath, "*.json"))
                {
                    var target = JObject.Parse(File.ReadAllText(path));
                    if (target["CurrentResults"] is JObject results &&
                        results["SolverSettings"] is JObject solverSettings)
                        solverSettings["AllowSpecialCakesForPassives"] ??= false;
                    StorageFile.WriteAtomic(path, target.ToString(Formatting.None), backup: true);
                }
            }
        }
    }
}
