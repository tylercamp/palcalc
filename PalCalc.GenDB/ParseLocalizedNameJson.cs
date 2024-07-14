using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    /* Note:
     * 
     * Files in i10n built locally from Palworld assets exported with FModel. Temporary solution until
     * BuildDBProgram2 is finished to just fetch these directly from game files
     * 
     * ('Pal/Content/L10N/{lang}/Pal/DataTable/Text' - DT_PalNameText and DT_SkillNameText)
     */

    class LocalizedNames
    {
        public class Entry
        {
            public string InternalName { get; set; }
            public string TranslatedName { get; set; }
        }

        public List<Entry> Pals { get; set; }
        public List<Entry> Traits { get; set; }

        public Dictionary<string, string> PalsByLowerInternalName => Pals.ToDictionary(e => e.InternalName.ToLower(), e => e.TranslatedName);
        public Dictionary<string, string> TraitsByLowerInternalName => Traits.ToDictionary(e => e.InternalName.ToLower(), e => e.TranslatedName);
    }

    internal static class ParseLocalizedNameJson
    {
        public static Dictionary<string, LocalizedNames> ParseLocalizedNames()
        {
            return Directory.EnumerateFiles("ref/i10n").ToDictionary(
                file => Path.GetFileNameWithoutExtension(file)!,
                file => JsonConvert.DeserializeObject<LocalizedNames>(File.ReadAllText(file))!
            );
        }
    }
}
