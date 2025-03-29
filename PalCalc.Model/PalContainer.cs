using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public interface IPalContainer
    {
        string Id { get; }
        LocationType Type { get; }
    }

    public class PalboxPalContainer : IPalContainer
    {
        public string Id { get; set; }
        public string PlayerId { get; set; }

        [JsonIgnore]
        public LocationType Type => LocationType.Palbox;
    }

    public class BasePalContainer : IPalContainer
    {
        public string Id { get; set; }
        public string BaseId { get; set; }

        [JsonIgnore]
        public LocationType Type => LocationType.Base;
    }

    public class PlayerPartyContainer : IPalContainer
    {
        public string Id { get; set; }
        public string PlayerId { get; set; }

        [JsonIgnore]
        public LocationType Type => LocationType.PlayerParty;
    }

    public class ViewingCageContainer : IPalContainer
    {
        public string Id { get; set; }
        public string BaseId { get; set; }

        [JsonIgnore]
        public LocationType Type => LocationType.ViewingCage;
    }

    public class DimensionalPalStorageContainer : IPalContainer
    {
        // note: doesn't actually have an ID, but need _something_ for properly tracking container membership. this can be
        //       based on player ID, main requirement is just consistency throughout the app
        public string Id { get; set; }
        public string PlayerId { get; set; }

        [JsonIgnore]
        public LocationType Type => LocationType.DimensionalPalStorage;
    }

    public class GlobalPalStorageContainer : IPalContainer
    {
        public string Id { get; set; }
        public string PlayerId { get; set; }

        [JsonIgnore]
        public LocationType Type => LocationType.GlobalPalStorage;
    }
}
