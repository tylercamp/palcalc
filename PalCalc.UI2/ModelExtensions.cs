using PalCalc.Model;
using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2
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

        public static string Identifier(this ISaveGame save) =>
            $"{save.UserId}-{save.GameId}";
    }
}
