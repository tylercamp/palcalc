using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.UI.Persistence;
using System.Collections.Generic;
using System.IO;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0002_DisableDisposableImplants : StorageMigration
    {
        // Frozen V2 values. Do not read these from runtime solver defaults: this list is part of
        // the migration contract and must not change when the live passive database changes.
        private static readonly string[] DisposableImplantPassiveInternalNames =
        [
            "SwimSpeed_up_3",
            "Vampire",
            "WorldTree_ATK",
            "MoveSpeed_up_3",
            "RideJumpCount_Increase2",
            "WorldTree_DEF",
            "CraftSpeed_up3",
            "PAL_FullStomach_Down_3",
            "MutationPal_Immortal",
            "MutationPal_Mutant",
            "WorldTree_Sanity",
            "MutationPal_ExplosionResist",
            "PAL_Sanity_Down_3",
            "WorldTree_ATK_DEF",
            "WorldTree_FullStomach",
            "Stamina_Up_3",
            "WorldTree_MoveSpeed",
            "Deffence_up3",
            "Demon's Hand",
            "MutationPal_Babysitter",
            "PAL_ALLAttack_up3",
        ];

        public V0002_DisableDisposableImplants() : base(1, 2) { }

        public override void Apply(StorageMigrationContext context)
        {
            var path = Path.Combine(context.DataPath, "settings.json");
            if (!File.Exists(path))
                return;

            var root = JObject.Parse(File.ReadAllText(path));
            var solverSettings = root["SolverSettings"] as JObject
                ?? throw new JsonSerializationException("Application settings have no SolverSettings object.");
            var banned = solverSettings["BannedSurgeryPassiveInternalNames"] as JArray
                ?? throw new JsonSerializationException("Application settings have no BannedSurgeryPassiveInternalNames array.");

            foreach (var internalName in DisposableImplantPassiveInternalNames)
            {
                if (!ContainsString(banned, internalName))
                    banned.Add(internalName);
            }

            solverSettings["BannedSurgeryPassiveInternalNames"] = banned;
            StorageFile.WriteAtomic(path, root.ToString(Formatting.None), backup: true);
        }

        private static bool ContainsString(IEnumerable<JToken> values, string expected)
        {
            foreach (var value in values)
            {
                if (value.Type == JTokenType.String && value.Value<string>() == expected)
                    return true;
            }

            return false;
        }
    }
}
