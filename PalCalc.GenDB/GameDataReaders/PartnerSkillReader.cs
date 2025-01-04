using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.Utils;
using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    public class RawPartnerSkill
    {
        public string BPClassName { get; set; }

        public List<RankEffect> RankEffects { get; set; }
        public class RankEffect
        {
            public List<string> PassiveSkillInternalNames { get; set; } = [];
        }
    }

    internal class PartnerSkillReader
    {
        private static ILogger logger = Log.ForContext<PartnerSkillReader>();

        public static List<RawPartnerSkill> ReadPartnerSkills(IFileProvider provider)
        {
            var res = new List<RawPartnerSkill>();
            foreach (var kvp in provider.Files.Where(kvp => kvp.Key.StartsWith(AssetPaths.PAL_BPS_BASE, StringComparison.InvariantCultureIgnoreCase)))
            {
                var bpName = kvp.Key.Replace(AssetPaths.PAL_BPS_BASE, "", StringComparison.InvariantCultureIgnoreCase).Split('/')[0];;
                if (!kvp.Key.EndsWith($"/BP_{bpName}.uasset", StringComparison.InvariantCultureIgnoreCase)) continue;

                if (!provider.TryLoadPackage(kvp.Value, out var ipkg))
                {
                    logger.Warning("Unable to load BP package at {path}, skipping", kvp.Key);
                    continue;
                }

                var pkg = ipkg as Package;
                if (!pkg.ExportMap.Any(e => e.ObjectName.Text == "PalPartnerSkillParameter_GEN_VARIABLE"))
                    continue;

                // note:
                // partner skill info is stored in a variety of ways. e.g. Mau passive skill
                // is stored via StaticCharacterParameterComponent.SpawnItem
                //
                // this is incomplete, but atm we really only care about partner skills
                // which involve passive skills (namely Yakumo's partner skill)

                var ps = pkg.GetExport("PalPartnerSkillParameter_GEN_VARIABLE", StringComparison.InvariantCultureIgnoreCase);
                if (ps == null)
                {
                    logger.Warning("Unable to find PalPartnerSkillParameter_GEN_VARIABLE for {path}, skipping", kvp.Key);
                    continue;
                }

                var passivesProperty = ps.Properties.FirstOrDefault(p => p.Name.Text == "PassiveSkills");
                if (passivesProperty == null) continue;

                var list = passivesProperty.Tag.GetValue<UScriptArray>();

                var rankEffects = list.Properties.Select(p =>
                {
                    var skillData = p.GetValue<FStructFallback>().Properties.FirstOrDefault(p => p.Name == "SkillAndParameters");
                    if (skillData == null) return null;

                    var skillNames = skillData
                        .Tag.GetValue<UScriptMap>()
                        .Properties.Select(p =>
                        {
                            return p.Key.GetValue<FStructFallback>().Properties.Single().Tag.GetValue<CUE4Parse.UE4.Objects.UObject.FName>().Text;
                        })
                        .ToList();

                    return new RawPartnerSkill.RankEffect() { PassiveSkillInternalNames = skillNames };
                }).SkipNull().ToList();

                res.Add(new RawPartnerSkill() { BPClassName = bpName, RankEffects = rankEffects });
            }

            return res;
        }
    }
}
