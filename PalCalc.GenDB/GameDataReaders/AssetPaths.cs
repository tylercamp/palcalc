using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    internal static class AssetPaths
    {
        public const string PASSIVE_SKILLS_PATH = "Pal/Content/Pal/DataTable/PassiveSkill/DT_PassiveSkill_Main";
        public const string PALS_PATH = "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter";
        public const string PALS_UNIQUE_BREEDING_PATH = "Pal/Content/Pal/DataTable/Character/DT_PalCombiUnique";

        public const string ACTIVE_SKILLS_PATH = "Pal/Content/Pal/DataTable/Waza/DT_WazaDataTable";
        public const string ACTIVE_SKILLS_PAL_LEVEL_PATH = "Pal/Content/Pal/DataTable/Waza/DT_WazaMasterLevel";

        public const string PAL_ICONS_MAPPING_PATH = "Pal/Content/Pal/DataTable/Character/DT_PalCharacterIconDataTable";
        public const string PAL_SPAWNERS_PATH = "Pal/Content/Pal/DataTable/Spawner/DT_PalWildSpawner";
        public const string PAL_CAGED_SPAWNERS_PATH = "Pal/Content/Pal/DataTable/Character/DT_CapturedCagePal";

        public const string LOCALIZATIONS_BASE = "Pal/Content/L10N";

        public const string MAP_IMAGE_PATH = "Pal/Content/Pal/Texture/UI/Map/T_WorldMap";
        public const string MAP_PROPERTIES_PATH = "Pal/Content/Pal/DataTable/WorldMapUIData/DT_WorldMapUIData";
    }
}
