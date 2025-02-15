using PalCalc.Model;
using PalCalc.UI.Localization;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.Model
{
    public class PassiveSkillsPreset
    {
        public string Name { get; set; }

        public string Passive1InternalName { get; set; }
        public string Passive2InternalName { get; set; }
        public string Passive3InternalName { get; set; }
        public string Passive4InternalName { get; set; }

        public string OptionalPassive1InternalName { get; set; }
        public string OptionalPassive2InternalName { get; set; }
        public string OptionalPassive3InternalName { get; set; }
        public string OptionalPassive4InternalName { get; set; }
    }

    public class SolverSettings
    {
        public int MaxBreedingSteps { get; set; } = 10;
        public int MaxSolverIterations { get; set; } = 20;
        public int MaxWildPals { get; set; } = 1;
        public int MaxInputIrrelevantPassives { get; set; } = 3;
        public int MaxBredIrrelevantPassives { get; set; } = 1;
        public int MaxThreads { get; set; } = 0;

        public List<string> BannedBredPalInternalNames { get; set; } = [];
        public List<string> BannedWildPalInternalNames { get; set; } = [
            "PlantSlime_Flower", // flower gumoss
        ];

        public List<Pal> BannedBredPals(PalDB db) => BannedBredPalInternalNames.Select(n => n.InternalToPal(db)).ToList();
        public List<Pal> BannedWildPals(PalDB db) => BannedWildPalInternalNames.Select(n => n.InternalToPal(db)).ToList();
    }

    public class AppSettings
    {
        public static AppSettings Current = null;

        public List<string> ExtraSaveLocations { get; set; } = new List<string>();

        public List<string> FakeSaveNames { get; set; } = new List<string>();

        public SolverSettings SolverSettings { get; set; } = new SolverSettings();

        public List<PassiveSkillsPreset> PassiveSkillsPresets { get; set; } = new List<PassiveSkillsPreset>();

        public string SelectedGameIdentifier { get; set; } = null;

        public TranslationLocale Locale { get; set; } = TranslationLocale.en;
    }
}
