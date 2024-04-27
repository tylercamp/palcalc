using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
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
            BreedingTimeMinutes = 10;
            MultipleBreedingFarms = true;
        }

        public GameSettingsViewModel(GameSettings modelObject)
        {
            BreedingTimeMinutes = (int)modelObject.BreedingTime.TotalMinutes;
            MultipleBreedingFarms = modelObject.MultipleBreedingFarms;
        }

        [JsonIgnore]
        public GameSettings ModelObject => new GameSettings()
        {
            BreedingTime = TimeSpan.FromMinutes(BreedingTimeMinutes),
            MultipleBreedingFarms = MultipleBreedingFarms,
        };

        [ObservableProperty]
        private int breedingTimeMinutes;

        [ObservableProperty]
        private bool multipleBreedingFarms;

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static GameSettingsViewModel FromJson(string json) => JsonConvert.DeserializeObject<GameSettingsViewModel>(json);

        public void Save(SaveGame forSave)
        {
            File.WriteAllText(Storage.GameSettingsPath(forSave), ToJson());
        }

        public static GameSettingsViewModel Load(SaveGame forSave)
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
