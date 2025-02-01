using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;

namespace PalCalc.UI.ViewModel.Mapped
{
    public partial class GameSettingsViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<GameSettingsViewModel>();

        // for XAML designer view
        public GameSettingsViewModel() : this(GameSettings.Defaults)
        {
        }

        public GameSettingsViewModel(GameSettings modelObject)
        {
            BreedingTimeSeconds = (int)modelObject.BreedingTime.TotalSeconds;
            MultipleBreedingFarms = modelObject.MultipleBreedingFarms;
            PalboxTabWidth = modelObject.LocationTypeGridWidths[LocationType.Palbox];
            PalboxTabHeight = modelObject.LocationTypeGridHeights[LocationType.Palbox].Value;
        }

        [JsonIgnore]
        public GameSettings ModelObject => new GameSettings()
        {
            BreedingTime = TimeSpan.FromSeconds(BreedingTimeSeconds),
            MultipleBreedingFarms = MultipleBreedingFarms,
            LocationTypeGridWidths = new()
            {
                { LocationType.Palbox, PalboxTabWidth },

                { LocationType.PlayerParty, GameSettings.Defaults.LocationTypeGridWidths[LocationType.PlayerParty] },
                { LocationType.ViewingCage, GameSettings.Defaults.LocationTypeGridWidths[LocationType.ViewingCage] },
                { LocationType.Base, GameSettings.Defaults.LocationTypeGridWidths[LocationType.Base] },
                { LocationType.Custom, GameSettings.Defaults.LocationTypeGridWidths[LocationType.Custom] },
            },
            LocationTypeGridHeights = new()
            {
                { LocationType.Palbox, PalboxTabHeight },

                { LocationType.PlayerParty, GameSettings.Defaults.LocationTypeGridHeights[LocationType.PlayerParty] },
                { LocationType.ViewingCage, GameSettings.Defaults.LocationTypeGridHeights[LocationType.ViewingCage] },
                { LocationType.Base, GameSettings.Defaults.LocationTypeGridHeights[LocationType.Base] },
                { LocationType.Custom, GameSettings.Defaults.LocationTypeGridHeights[LocationType.Custom] },
            },
        };

        [ObservableProperty]
        private int breedingTimeSeconds;

        [ObservableProperty]
        private bool multipleBreedingFarms;

        [ObservableProperty]
        private int palboxTabWidth;

        [ObservableProperty]
        private int palboxTabHeight;

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static GameSettingsViewModel FromJson(string json)
        {
            var parsedJson = JsonConvert.DeserializeObject<JToken>(json);

            var result = parsedJson.ToObject<GameSettingsViewModel>();
            if (parsedJson["BreedingTimeMinutes"] != null)
            {
                result.BreedingTimeSeconds = 60 * parsedJson["BreedingTimeMinutes"].ToObject<int>();
            }

            return result;
        }

        public void Save(ISaveGame forSave)
        {
            File.WriteAllText(Storage.GameSettingsPath(forSave), ToJson());
        }

        public static GameSettingsViewModel Load(ISaveGame forSave)
        {
            var path = Storage.GameSettingsPath(forSave);
            if (File.Exists(path))
            {
                try
                {
                    return FromJson(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "error when loading game settings from {path}", path);
                    return new GameSettingsViewModel();
                }
            }
            else
            {
                return new GameSettingsViewModel();
            }
        }
    }
}
