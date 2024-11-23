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
        public static ILocalizedText Label(this LocationType locType) =>
            locType switch
            {
                LocationType.Palbox => LocalizationCodes.LC_PAL_LOC_PALBOX.Bind(),
                LocationType.Base => LocalizationCodes.LC_PAL_LOC_BASE.Bind(),
                LocationType.PlayerParty => LocalizationCodes.LC_PAL_LOC_PARTY.Bind(),
                LocationType.ViewingCage => new HardCodedText("Viewing Cage"), // TODO
                LocationType.Custom => LocalizationCodes.LC_PAL_LOC_CUSTOM.Bind(),
                _ => throw new NotImplementedException()
            };

        public static ILocalizedText Label(this PalGender gender) =>
            gender switch
            {
                PalGender.FEMALE => LocalizationCodes.LC_COMMON_GENDER_FEMALE.Bind(),
                PalGender.MALE => LocalizationCodes.LC_COMMON_GENDER_MALE.Bind(),
                PalGender.WILDCARD => LocalizationCodes.LC_COMMON_GENDER_WILDCARD.Bind(),
                PalGender.OPPOSITE_WILDCARD => LocalizationCodes.LC_COMMON_GENDER_OPPOSITE_WILDCARD.Bind(),
                _ => throw new NotImplementedException()
            };

        public static PassiveSkillsPreset ToPreset(this PalSpecifierViewModel spec) =>
            new()
            {
                Passive1InternalName = spec.Passive1?.ModelObject?.InternalName,
                Passive2InternalName = spec.Passive2?.ModelObject?.InternalName,
                Passive3InternalName = spec.Passive3?.ModelObject?.InternalName,
                Passive4InternalName = spec.Passive4?.ModelObject?.InternalName,

                OptionalPassive1InternalName = spec.OptionalPassive1?.ModelObject?.InternalName,
                OptionalPassive2InternalName = spec.OptionalPassive2?.ModelObject?.InternalName,
                OptionalPassive3InternalName = spec.OptionalPassive3?.ModelObject?.InternalName,
                OptionalPassive4InternalName = spec.OptionalPassive4?.ModelObject?.InternalName,
            };
    }
}
