using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class UPalSpawner
    {
        [FStructProperty]
        public string Pal_1 { get; set; }
        [FStructProperty]
        public int LvMin_1 { get; set; }
        [FStructProperty]
        public int LvMax_1 { get; set; }

        [FStructProperty]
        public string Pal_2 { get; set; }
        [FStructProperty]
        public int LvMin_2 { get; set; }
        [FStructProperty]
        public int LvMax_2 { get; set; }

        [FStructProperty]
        public string Pal_3 { get; set; }
        [FStructProperty]
        public int LvMin_3 { get; set; }
        [FStructProperty]
        public int LvMax_3 { get; set; }

        public IEnumerable<(string, int, int)> Entries()
        {
            if (Pal_1 != "None")
                yield return (Pal_1, LvMin_1, LvMax_1);

            if (Pal_2 != "None")
                yield return (Pal_2, LvMin_2, LvMax_2);

            if (Pal_3 != "None")
                yield return (Pal_3, LvMin_3, LvMax_3);
        }
    }
    class UCagedPalSpawner
    {
        [FStructProperty]
        public string PalID { get; set; }
        [FStructProperty]
        public int MinLevel { get; set; }
        [FStructProperty]
        public int MaxLevel { get; set; }
    }

    internal class PalSpawnerReader
    {
        private static ILogger logger = Log.ForContext<PalSpawnerReader>();

        static string TrimPalId(string palId) =>
            palId
                .Replace("Boss_", "", StringComparison.InvariantCultureIgnoreCase)
                .Replace("Raid_", "", StringComparison.InvariantCultureIgnoreCase)
                .Replace("Gym_", "", StringComparison.InvariantCultureIgnoreCase);

        public static Dictionary<string, (int, int)> ReadWildLevelRanges(IFileProvider provider)
        {
            logger.Information("Reading wild pal level ranges");
            var rawWildSpawnEntries = provider.LoadPackageObject<UDataTable>(AssetPaths.PAL_SPAWNERS_PATH);
            var rawCagedSpawnEntries = provider.LoadPackageObject<UDataTable>(AssetPaths.PAL_CAGED_SPAWNERS_PATH);

            var rawWildLevels = rawWildSpawnEntries.RowMap.SelectMany(entry => entry.Value.ToObject<UPalSpawner>().Entries());
            var rawCagedLevels = rawCagedSpawnEntries.RowMap.Select(entry => entry.Value.ToObject<UCagedPalSpawner>()).Select(e => (e.PalID, e.MinLevel, e.MaxLevel));

            // this seems to cover *almost* every way a pal can be spawned, other than:
            // - raids on player bases
            // - raid bosses

            return rawWildLevels.Concat(rawCagedLevels)
                .GroupBy(s => TrimPalId(s.Item1), StringComparer.InvariantCultureIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Aggregate(
                        // (explicit cast to tuple to remove 'MaxValue', 'MinValue' anonymous fields, which were
                        // confusing since they stored the opposite data you'd expect from the name)
                        ((int, int))(int.MaxValue, int.MinValue),
                        (accum, e) => (
                            Math.Min(accum.Item1, e.Item2),
                            Math.Max(accum.Item2, e.Item3)
                        )
                    )
                );
        }
    }
}
