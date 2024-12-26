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
        public GameSettingsViewModel()
        {
            BreedingTimeSeconds = (int)new GameSettings().BreedingTime.TotalSeconds;
            MultipleBreedingFarms = true;
        }

        public GameSettingsViewModel(GameSettings modelObject)
        {
            BreedingTimeSeconds = (int)modelObject.BreedingTime.TotalSeconds;
            MultipleBreedingFarms = modelObject.MultipleBreedingFarms;
        }

        [JsonIgnore]
        public GameSettings ModelObject => new GameSettings()
        {
            BreedingTime = TimeSpan.FromSeconds(BreedingTimeSeconds),
            MultipleBreedingFarms = MultipleBreedingFarms,
        };

        [ObservableProperty]
        private int breedingTimeSeconds;

        [ObservableProperty]
        private bool multipleBreedingFarms;

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
