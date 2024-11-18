using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Versions;
using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 * To get the latest usmap file:
 * 
 * 1. Download the latest UE4SS dev build: https://github.com/UE4SS-RE/RE-UE4SS/releases
 *       "zDEV-UE4SS...zip"
 * 
 * 2. Go to Palworld install dir, copy contents directly next to Palworld-Win64-Shipping.exe
 * 
 * 3. Run the game, secondary windows pop up in background, one of them will be "UE4SS Debugging Tools"
 * 
 * 4. Go to "Dumpers" tab, click "Generate .usmap file..."
 * 
 * 5. Copy "Mappings.usmap" file created next to "Palworld-Win64-Shipping.exe"
 * 
 * (Delete / rename "dwmapi.dll" to effectively disable)
 */

namespace PalCalc.GenDB
{
    static class BuildDBProgram2
    {
        /*
         * Shelved while working on translations
         */

        // This is all HEAVILY dependent on having the right Mappings.usmap file for the Palworld version!
        static string PalworldDirPath = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld";
        static string MappingsPath = @"C:\Users\algor\OneDrive\Desktop\Mappings.usmap";

        /*
         [INF] Successfully saved Pal/Content/L10N
         [INF] Successfully saved Pal/Content/Pal/DataTable/Waza
         [INF] Successfully saved Pal/Content/Pal/DataTable/PassiveSkill
         [INF] Successfully saved Pal/Content/Pal/DataTable/Character
         [INF] Successfully saved Pal/Content/Pal/Texture/PalIcon/Normal
         */

        const string PASSIVE_SKILLS_PATH = "Pal/Content/Pal/DataTable/PassiveSkill/DT_PassiveSkill_Main";
        const string PALS_PATH = "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter";

        private static void FetchLocalizations(IFileProvider provider)
        {
            var L10N_BASE_PATH = "Pal/Content/L10N";
            var l10nAssets = provider.Files.Where(kvp => kvp.Key.StartsWith(L10N_BASE_PATH, StringComparison.InvariantCultureIgnoreCase)).ToList();

            var langs = l10nAssets.Select(kvp => kvp.Key.Substring(L10N_BASE_PATH.Length + 1).Split('/').First()).Distinct().ToList();
        }

        private static List<PassiveSkill> ReadPassiveSkills(IFileProvider provider)
        {
            var rawPassives = provider.LoadObject<UDataTable>(PASSIVE_SKILLS_PATH);

            return null;
        }

        private static List<Pal> ReadPals(IFileProvider provider)
        {
            var rawPals = provider.LoadObject<UDataTable>(PALS_PATH);

            return null;
        }

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            var provider = new DefaultFileProvider(PalworldDirPath, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_1));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(MappingsPath);

            provider.Initialize();
            provider.Mount();
            provider.LoadVirtualPaths();

            provider.LoadLocalization();

            FetchLocalizations(provider);

            ReadPassiveSkills(provider);
            ReadPals(provider);

            var allFiles = provider.Files.ToList();

            var palFiles = allFiles.Where(f => f.Key.StartsWith("Pal/Content", StringComparison.InvariantCultureIgnoreCase)).ToList();

            var traits = provider.LoadAllObjects("pal/content/pal/datatable/passiveskill/dt_passiveskill_main").First() as UDataTable;

            var palData = provider.LoadAllObjects("Pal/Content/Pal/DataTable/Character");
            var icons = provider.LoadAllObjects("Pal/Content/Pal/Texture/PalIcon/Normal");
        }
    }
}
