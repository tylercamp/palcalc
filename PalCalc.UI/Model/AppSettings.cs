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

    public class PalListPreset
    {
        public string Name { get; set; }

        public List<string> PalInternalNames { get; set; }
    }

    public class SerializableSolverSettings
    {
        public int MaxBreedingSteps { get; set; } = 10;
        public int MaxSolverIterations { get; set; } = 20;
        public int MaxWildPals { get; set; } = 1;
        public int MaxInputIrrelevantPassives { get; set; } = 3;
        public int MaxBredIrrelevantPassives { get; set; } = 1;
        public int MaxThreads { get; set; } = 0;
        public int MaxGoldCost { get; set; } = 0;
        public bool UseGenderReversers { get; set; } = false;

        public List<string> BannedBredPalInternalNames { get; set; } = [];
        public List<string> BannedWildPalInternalNames { get; set; } = [
            "PlantSlime_Flower", // flower gumoss
        ];

        public List<string> BannedSurgeryPassiveInternalNames { get; set; } = [
            // Passives from Disposable Implants, which require specific items
            // (list as of 8/2026)

            "SwimSpeed_up_3", // King of the Waves
            "Vampire", // Vampiric
            "WorldTree_ATK", // Twin-Edged Holy Blade
            "MoveSpeed_up_3", // Swift
            "RideJumpCount_Increase2", // Sky Strider
            "WorldTree_DEF", // Sanctified Meat Shield
            "CraftSpeed_up3", // Remarkable Craftsmanship
            "PAL_FullStomach_Down_3", // Mastery of Fasting
            "MutationPal_Immortal", // Immortality
            "MutationPal_Mutant", // Idiosyncratic
            "WorldTree_Sanity", // Hermit Sage
            "MutationPal_ExplosionResist", // Heavily Armored
            "PAL_Sanity_Down_3", // Heart of the Immovable King
            "WorldTree_ATK_DEF", // God of Destruction
            "WorldTree_FullStomach", // World Tree's Bounty
            "Stamina_Up_3", // Eternal Engine
            "WorldTree_MoveSpeed", // Dimensional Leap
            "Deffence_up3", // Diamond Body
            "Demon's Hand", // Demon's Hand
            "MutationPal_Babysitter", // Babysitter
            "PAL_ALLAttack_up3", // Demon God
        ];

        public List<Pal> BannedBredPals(PalDB db) => BannedBredPalInternalNames.Select(n => n.InternalToPal(db)).ToList();
        public List<Pal> BannedWildPals(PalDB db) => BannedWildPalInternalNames.Select(n => n.InternalToPal(db)).ToList();

        public List<PassiveSkill> BannedSurgeryPassives(PalDB db) => BannedSurgeryPassiveInternalNames.Select(n => n.InternalToStandardPassive(db)).ToList();
    }

    public class BreedingResultListColumnSettings
    {
        // If key exists: true = force visible, false = force hidden
        // If key doesn't exist: auto (follow ViewModel)
        public Dictionary<string, bool> ColumnVisibility { get; set; } = new();
        public List<string> ColumnOrder { get; set; } = new();
    }

    public class WindowPlacementSettings
    {
        public int Version { get; set; } = 1;

        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public bool IsMaximized { get; set; }
    }

    public enum LayoutGridUnit
    {
        Auto,
        Pixel,
        Star,
    }

    public class GridLengthSettings
    {
        public LayoutGridUnit Unit { get; set; }
        public double Value { get; set; }
    }

    public class GridLayoutSettings
    {
        public int Version { get; set; } = 1;
        public List<GridLengthSettings> Columns { get; set; } = new();
        public List<GridLengthSettings> Rows { get; set; } = new();
    }

    public class UiLayoutSettings
    {
        public Dictionary<string, WindowPlacementSettings> Windows { get; set; } = new();
        public Dictionary<string, GridLayoutSettings> Grids { get; set; } = new();
    }

    public class AppSettings
    {
        public static AppSettings Current = null;

        public List<string> ExtraSaveLocations { get; set; } = [];

        public List<string> FakeSaveNames { get; set; } = [];

        public SerializableSolverSettings SolverSettings { get; set; } = new();

        public List<PassiveSkillsPreset> PassiveSkillsPresets { get; set; } = [];

        public List<PalListPreset> PalListPresets { get; set; } = [];

        public string SelectedGameIdentifier { get; set; } = null;

        public TranslationLocale Locale { get; set; } = TranslationLocale.en;

        public bool IsDarkTheme { get; set; } = true;

        public BreedingResultListColumnSettings BreedingResultListColumns { get; set; } = new();

        public UiLayoutSettings UiLayout { get; set; } = new();

        public string SkippedAppVersion { get; set; } = null;
    }
}
