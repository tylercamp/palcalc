using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI
{
    internal static class ModelExtensions
    {
        public static string Label(this LocationType locType) =>
            locType switch
            {
                LocationType.Palbox => "Palbox",
                LocationType.Base => "Base",
                LocationType.PlayerParty => "Party",
                _ => throw new NotImplementedException()
            };

        public static string Label(this PalGender gender) =>
            gender switch
            {
                PalGender.FEMALE => "Female",
                PalGender.MALE => "Male",
                PalGender.WILDCARD => "Any Gender",
                PalGender.OPPOSITE_WILDCARD => "Opposite Gender",
                _ => throw new NotImplementedException()
            };
    }
}
