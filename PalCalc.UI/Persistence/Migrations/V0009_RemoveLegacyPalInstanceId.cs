using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0009_RemoveLegacyPalInstanceId : StorageMigration
    {
        public V0009_RemoveLegacyPalInstanceId() : base(8, 9) { }

        public override void Apply(StorageMigrationContext context)
        {
            foreach (var path in Directory.EnumerateFiles(context.DataPath, "custom-containers.json", SearchOption.AllDirectories))
                RemoveLegacyIds(path, document => document.SelectTokens("CustomContainers[*].Contents[*]"));

            foreach (var targetsPath in Directory.EnumerateDirectories(context.DataPath, "targets", SearchOption.AllDirectories))
                foreach (var path in Directory.EnumerateFiles(targetsPath, "*.json"))
                    RemoveLegacyIds(path, document => document.SelectTokens("CurrentResults.Results[*].PalReference")
                        .OfType<JObject>()
                        .SelectMany(reference => reference.DescendantsAndSelf().OfType<JObject>())
                        .Where(reference => reference["RefType"] is JValue { Value: "OWNED_PAL" })
                        .Select(reference => reference["Instance"]));
        }

        private static void RemoveLegacyIds(string path, Func<JObject, IEnumerable<JToken>> selectSnapshots)
        {
            var document = JObject.Parse(File.ReadAllText(path));
            var snapshots = selectSnapshots(document).OfType<JObject>().ToArray();
            var changed = false;
            foreach (var snapshot in snapshots)
                changed |= snapshot.Remove("InstanceId");

            if (changed)
            {
                // Keep a copy of the v8 document even when an earlier migration left a backup.
                var backupSuffix = File.Exists(path + ".bak") ? ".v8.bak" : ".bak";
                StorageFile.WriteAtomic(path, document.ToString(Formatting.None), backup: true, backupSuffix: backupSuffix);
            }
        }
    }
}
