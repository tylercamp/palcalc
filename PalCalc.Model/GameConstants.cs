using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class GameConstants
    {
        // TODO - Could parse cake effects from DA_BreedingItemEffectData
        // [{ TalentBonusMin, TalenBonusMax, MutationRateBonusPercent, CombiRankBonus, BreedCount, bInheritAllActiveSkills, PassiveInheritCountOverride }]

        // TODO - Could scrape from PalEggRankInfoArray? [{ PalRarity, EggScale, HatchingSpeedDivisionRate }]
        public static readonly Dictionary<EggSize, int> EggSizeMinRarity = new()
        {
            // couldn't find this info when scraping through game data, found by checking against https://paldb.cc/en/Eggs
            // and saw that Rarity correlates with egg size
            { EggSize.Normal, 0 },
            { EggSize.Large, 5 },
            { EggSize.Huge, 8 },
        };

        public static readonly int MaxTotalPassives = 4;
    }
}
