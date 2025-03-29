using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI
{
    internal static class ModelExtensions
    {
        public static ILocalizedText ShortLabel(this LocationType locType) =>
            locType switch
            {
                LocationType.Palbox => LocalizationCodes.LC_PAL_LOC_PALBOX.Bind(),
                LocationType.Base => LocalizationCodes.LC_PAL_LOC_BASE.Bind(),
                LocationType.PlayerParty => LocalizationCodes.LC_PAL_LOC_PARTY.Bind(),
                LocationType.ViewingCage => LocalizationCodes.LC_PAL_LOC_VIEWING_CAGE.Bind(),
                LocationType.Custom => LocalizationCodes.LC_PAL_LOC_CUSTOM.Bind(),
                LocationType.DimensionalPalStorage => LocalizationCodes.LC_PAL_LOC_DPS_SHORT.Bind(),
                _ => throw new NotImplementedException()
            };

        public static ILocalizedText FullLabel(this LocationType locType) =>
            locType switch
            {
                LocationType.Palbox => LocalizationCodes.LC_PAL_LOC_PALBOX.Bind(),
                LocationType.Base => LocalizationCodes.LC_PAL_LOC_BASE.Bind(),
                LocationType.PlayerParty => LocalizationCodes.LC_PAL_LOC_PARTY.Bind(),
                LocationType.ViewingCage => LocalizationCodes.LC_PAL_LOC_VIEWING_CAGE.Bind(),
                LocationType.Custom => LocalizationCodes.LC_PAL_LOC_CUSTOM.Bind(),
                LocationType.DimensionalPalStorage => LocalizationCodes.LC_PAL_LOC_DPS_FULL.Bind(),
                _ => throw new NotImplementedException()
            };

        public static ILocalizedText Label(this PalGender gender) =>
            gender switch
            {
                PalGender.FEMALE => LocalizationCodes.LC_COMMON_GENDER_FEMALE.Bind(),
                PalGender.MALE => LocalizationCodes.LC_COMMON_GENDER_MALE.Bind(),
                PalGender.WILDCARD => LocalizationCodes.LC_COMMON_GENDER_WILDCARD.Bind(),
                PalGender.OPPOSITE_WILDCARD => LocalizationCodes.LC_COMMON_GENDER_OPPOSITE_WILDCARD.Bind(),
                PalGender.NONE => LocalizationCodes.LC_COMMON_GENDER_NONE.Bind(),
                _ => throw new NotImplementedException()
            };

        public static PassiveSkillsPreset ToPreset(this PalSpecifierViewModel spec) =>
            new()
            {
                Passive1InternalName = spec.RequiredPassives.Passive1?.ModelObject?.InternalName,
                Passive2InternalName = spec.RequiredPassives.Passive2?.ModelObject?.InternalName,
                Passive3InternalName = spec.RequiredPassives.Passive3?.ModelObject?.InternalName,
                Passive4InternalName = spec.RequiredPassives.Passive4?.ModelObject?.InternalName,

                OptionalPassive1InternalName = spec.OptionalPassives.Passive1?.ModelObject?.InternalName,
                OptionalPassive2InternalName = spec.OptionalPassives.Passive2?.ModelObject?.InternalName,
                OptionalPassive3InternalName = spec.OptionalPassives.Passive3?.ModelObject?.InternalName,
                OptionalPassive4InternalName = spec.OptionalPassives.Passive4?.ModelObject?.InternalName,
            };
    }
}
