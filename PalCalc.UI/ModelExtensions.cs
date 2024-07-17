using PalCalc.Model;
using PalCalc.UI.Localization;
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
    }
}
