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
            MassiveEggIncubationTimeMinutes = (int)modelObject.MassiveEggIncubationTime.TotalMinutes;
            MultipleBreedingFarms = modelObject.MultipleBreedingFarms;
            PalboxTabWidth = modelObject.LocationTypeGridWidths[LocationType.Palbox];
            PalboxTabHeight = modelObject.LocationTypeGridHeights[LocationType.Palbox].Value;

            AppRestartRequired = false;
        }

        [JsonIgnore]
        public GameSettings ModelObject => new GameSettings()
        {
            BreedingTime = TimeSpan.FromSeconds(BreedingTimeSeconds),
            MassiveEggIncubationTime = TimeSpan.FromMinutes(MassiveEggIncubationTimeMinutes),
            MultipleBreedingFarms = MultipleBreedingFarms,
            LocationTypeGridWidths = new()
            {
                { LocationType.Palbox, PalboxTabWidth },

                { LocationType.DimensionalPalStorage, GameSettings.Defaults.LocationTypeGridWidths[LocationType.DimensionalPalStorage] },
                { LocationType.GlobalPalStorage, GameSettings.Defaults.LocationTypeGridWidths[LocationType.GlobalPalStorage] },
                { LocationType.PlayerParty, GameSettings.Defaults.LocationTypeGridWidths[LocationType.PlayerParty] },
                { LocationType.ViewingCage, GameSettings.Defaults.LocationTypeGridWidths[LocationType.ViewingCage] },
                { LocationType.Base, GameSettings.Defaults.LocationTypeGridWidths[LocationType.Base] },
                { LocationType.Custom, GameSettings.Defaults.LocationTypeGridWidths[LocationType.Custom] },
            },
            LocationTypeGridHeights = new()
            {
                { LocationType.Palbox, PalboxTabHeight },

                { LocationType.DimensionalPalStorage, GameSettings.Defaults.LocationTypeGridHeights[LocationType.DimensionalPalStorage] },
                { LocationType.GlobalPalStorage, GameSettings.Defaults.LocationTypeGridHeights[LocationType.GlobalPalStorage] },
                { LocationType.PlayerParty, GameSettings.Defaults.LocationTypeGridHeights[LocationType.PlayerParty] },
                { LocationType.ViewingCage, GameSettings.Defaults.LocationTypeGridHeights[LocationType.ViewingCage] },
                { LocationType.Base, GameSettings.Defaults.LocationTypeGridHeights[LocationType.Base] },
                { LocationType.Custom, GameSettings.Defaults.LocationTypeGridHeights[LocationType.Custom] },
            },
        };

        [ObservableProperty]
        private int breedingTimeSeconds;

        [ObservableProperty]
        private int massiveEggIncubationTimeMinutes;

        [ObservableProperty]
        private bool multipleBreedingFarms;

        private int palboxTabWidth;
        public int PalboxTabWidth
        {
            get => palboxTabWidth;
            set
            {
                if (SetProperty(ref palboxTabWidth, value))
                {
                    AppRestartRequired = true;
                    OnPropertyChanged(nameof(AppRestartRequired));
                }
            }
        }

        private int palboxTabHeight;
        public int PalboxTabHeight
        {
            get => palboxTabHeight;
            set
            {
                if (SetProperty(ref palboxTabHeight, value))
                {
                    AppRestartRequired = true;
                    OnPropertyChanged(nameof(AppRestartRequired));
                }
            }
        }

        public bool AppRestartRequired { get; private set; } = false;

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static GameSettingsViewModel FromJson(string json)
        {
            var parsedJson = JsonConvert.DeserializeObject<JToken>(json);

            var result = parsedJson.ToObject<GameSettingsViewModel>();
            if (parsedJson["BreedingTimeMinutes"] != null)
            {
                result.BreedingTimeSeconds = 60 * parsedJson["BreedingTimeMinutes"].ToObject<int>();
            }

            // if we're missing this field, then the data was made before we accounted for incubation times,
            // which is the same as setting to 0
            if (parsedJson["MassiveEggIncubationTimeMinutes"] == null)
            {
                result.MassiveEggIncubationTimeMinutes = 0;
            }

            result.AppRestartRequired = false;
            return result;
        }

        public void Save(ISaveGame forSave)
        {
            if (Storage.DEBUG_DisableStorage) return;

            File.WriteAllText(Storage.GameSettingsPath(forSave), ToJson());
        }

        public static GameSettingsViewModel Load(ISaveGame forSave)
        {
            if (Storage.DEBUG_DisableStorage) return new GameSettingsViewModel();

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
