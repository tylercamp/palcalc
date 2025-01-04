using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class UPassiveSkill
    {
        // (note: there's an OverrideNameTextId which is always set to "None" for passive skills, though it's sometimes set for UPal data)
        [FStructProperty]
        public int Rank { get; set; }

        [FStructProperty]
        public bool AddPal { get; set; }

        [FStructProperty]
        public bool AddRarePal { get; set; }

        [FStructProperty]
        public int LotteryWeight { get; set; }

        #region Passive Skill Effects
        [FStructProperty]
        public string TargetType1 { get; set; }
        [FStructProperty]
        public string EffectType1 { get; set; }
        [FStructProperty]
        public float EffectValue1 { get; set; }

        [FStructProperty]
        public string TargetType2 { get; set; }
        [FStructProperty]
        public string EffectType2 { get; set; }
        [FStructProperty]
        public float EffectValue2 { get; set; }

        [FStructProperty]
        public string TargetType3 { get; set; }
        [FStructProperty]
        public string EffectType3 { get; set; }
        [FStructProperty]
        public float EffectValue3 { get; set; }
        #endregion

        // (assigned manually)
        public string InternalName { get; set; }
    }

    internal class PassiveSkillsReader
    {
        private static ILogger logger = Log.ForContext<PassiveSkillsReader>();

        public static List<UPassiveSkill> ReadPassiveSkills(IFileProvider provider, List<string> extraPassives)
        {
            logger.Information("Reading passive skills");
            var rawPassives = provider.LoadObject<UDataTable>(AssetPaths.PASSIVE_SKILLS_PATH);

            var res = new List<UPassiveSkill>();
            foreach (var row in rawPassives.RowMap)
            {
                var skill = row.Value.ToObject<UPassiveSkill>();
                if (!skill.AddPal && !skill.AddRarePal && !extraPassives.Contains(row.Key.Text)) continue;

                skill.InternalName = row.Key.Text;
                res.Add(skill);
            }

            return res;
        }
    }
}
