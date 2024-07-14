using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Versions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    static class BuildDBProgram2
    {
        // This is all HEAVILY dependent on having the right Mappings.usmap file for the Palworld version!
        static string PalworldDirPath = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld";
        static string MappingsPath = @"C:\Users\algor\Downloads\Mappings.usmap";

        /*
         [INF] Successfully saved Pal/Content/L10N
         [INF] Successfully saved Pal/Content/Pal/DataTable/Waza
         [INF] Successfully saved Pal/Content/Pal/DataTable/PassiveSkill
         [INF] Successfully saved Pal/Content/Pal/DataTable/Character
         [INF] Successfully saved Pal/Content/Pal/Texture/PalIcon/Normal
         */

        private static void FetchLocalizations(IFileProvider provider)
        {
            var L10N_BASE_PATH = "Pal/Content/L10N";
            var l10nAssets = provider.Files.Where(kvp => kvp.Key.StartsWith(L10N_BASE_PATH, StringComparison.InvariantCultureIgnoreCase)).ToList();

            var langs = l10nAssets.Select(kvp => kvp.Key.Substring(L10N_BASE_PATH.Length + 1).Split('/').First()).Distinct().ToList();
        }

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            var provider = new DefaultFileProvider(PalworldDirPath, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_1));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(MappingsPath);

            provider.Initialize();
            provider.Mount();
            provider.LoadVirtualPaths();

            FetchLocalizations(provider);

            var traits = provider.LoadAllObjects("Pal/Content/Pal/DataTable/PassiveSkill");
            var palData = provider.LoadAllObjects("Pal/Content/Pal/DataTable/Character");
            var icons = provider.LoadAllObjects("Pal/Content/Pal/Texture/PalIcon/Normal");
        }
    }
}
