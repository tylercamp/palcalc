using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using Microsoft.VisualBasic;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class UActiveSkillLevel
    {
        [FStructProperty]
        public string PalID { get; set; }
        [FStructProperty]
        public string WazaID { get; set; }
    }

    class UActiveSkill
    {
        [FStructProperty]
        public string WazaType { get; set; }

        [FStructProperty]
        public string Element { get; set; }

        [FStructProperty]
        public int Power { get; set; }

        [FStructProperty]
        public float CoolTime { get; set; }

        [FStructProperty]
        public bool DisabledData { get; set; }

        [FStructProperty] // TODO - not sure what this actually does
        public bool IgnoreRandomInherit { get; set; }
    }

    internal class ActiveSkillReader
    {
        private static ILogger logger = Log.ForContext<ActiveSkillReader>();

        public static List<UActiveSkill> ReadActiveSkills(IFileProvider provider)
        {
            logger.Information("Reading active skills");

            var rawAttackLevels = provider.LoadObject<UDataTable>(AssetPaths.ACTIVE_SKILLS_PAL_LEVEL_PATH);
            var availableAttackIds = rawAttackLevels.RowMap.Select(r => r.Value.ToObject<UActiveSkillLevel>().WazaID).Distinct().ToList();

            var rawAttacks = provider.LoadObject<UDataTable>(AssetPaths.ACTIVE_SKILLS_PATH);

            var res = new List<UActiveSkill>();
            foreach (var row in rawAttacks.RowMap)
            {
                var attack = row.Value.ToObject<UActiveSkill>();
                if (attack.DisabledData || !availableAttackIds.Contains(attack.WazaType)) continue;

                res.Add(attack);
            }

            return res;
        }
    }
}
