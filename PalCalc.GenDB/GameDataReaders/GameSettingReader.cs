using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.GenDB.GameDataReaders
{
    public class RawGameSettings
    {
        // weight arrays governing breeding inheritance (see PalCalc.Model.GameConstants)
        public List<int> TalentInheritNum { get; set; }
        public List<int> PassiveInheritNum { get; set; }
        public List<int> PassiveRandomAddNum { get; set; }
    }

    internal class GameSettingReader
    {
        private static ILogger logger = Log.ForContext<GameSettingReader>();

        public static RawGameSettings ReadGameSettings(IFileProvider provider)
        {
            // The values live on the PalGameSetting CDO (BP_PalGameSetting). We don't hardcode the
            // exact asset path — instead scan any "PalGameSetting" asset for the export that actually
            // carries `Combi_TalentInheritNum`, which is robust to path/name changes across versions.
            var candidates = provider.Files
                .Where(kvp => kvp.Key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                           && kvp.Key.Contains("PalGameSetting", StringComparison.OrdinalIgnoreCase));

            foreach (var kvp in candidates)
            {
                if (!provider.TryLoadPackage(kvp.Value, out var ipkg))
                    continue;

                foreach (var obj in ipkg.GetExports())
                {
                    if (!obj.Properties.Any(p => p.Name.Text == "Combi_TalentInheritNum"))
                        continue;

                    logger.Information("Reading breeding inheritance settings from {path}", kvp.Key);
                    return new RawGameSettings()
                    {
                        TalentInheritNum = ReadIntArray(obj, "Combi_TalentInheritNum"),
                        PassiveInheritNum = ReadIntArray(obj, "Combi_PassiveInheritNum"),
                        PassiveRandomAddNum = ReadIntArray(obj, "Combi_PassiveRandomAddNum"),
                    };
                }
            }

            logger.Warning("Could not find PalGameSetting with Combi_TalentInheritNum; inheritance constants will use datamined defaults.");
            return null;
        }

        private static List<int> ReadIntArray(UObject obj, string name)
        {
            var prop = obj.Properties.FirstOrDefault(p => p.Name.Text == name);
            if (prop == null)
            {
                logger.Warning("PalGameSetting property {name} not found", name);
                return null;
            }

            var arr = prop.Tag?.GetValue<UScriptArray>();
            if (arr == null) return null;

            // stored as floats (e.g. 3.0, 2.0, 1.0); these are integer weights
            return arr.Properties.Select(p => (int)Math.Round(p.GetValue<float>())).ToList();
        }
    }
}
