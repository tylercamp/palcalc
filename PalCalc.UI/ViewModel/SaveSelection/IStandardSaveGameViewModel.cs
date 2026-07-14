using Newtonsoft.Json.Linq;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    abstract class IStandardSaveGameViewModel : ISaveGameViewModel2
    {
        private static ILogger logger = Log.ForContext<IStandardSaveGameViewModel>();

        public IStandardSaveGameViewModel(StandardSaveGame standardSave) : this((ISaveGame)standardSave)
        {
            // For Steam / manual saves
        }

        public IStandardSaveGameViewModel(XboxSaveGame xboxSave) : this((ISaveGame)xboxSave)
        {
            // For Xbox saves specifically
        }

        private IStandardSaveGameViewModel(ISaveGame save)
        {
            ModelObject = save;
            IsValid = save.IsValid;

            try
            {
                var meta = ModelObject.LevelMeta.ReadGameOptions();
                WorldName = meta.WorldName;
                DayNumber = meta.InGameDay;

                if (meta.IsServerSave)
                {
                    CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(new
                    {
                        DayNumber = meta.InGameDay,
                        WorldName = meta.WorldName,
                    });
                }
                else
                {
                    MainPlayerName = meta.PlayerName;
                    MainPlayerLevel = meta.PlayerLevel;

                    CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL.Bind(new
                    {
                        PlayerName = meta.PlayerName,
                        PlayerLevel = meta.PlayerLevel,
                        DayNumber = meta.InGameDay,
                        WorldName = meta.WorldName,
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "error when loading LevelMeta for {saveId}", CachedSaveGame.IdentifierFor(ModelObject));
                CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_NO_METADATA.Bind(ModelObject.GameId);
            }

            Warnings = [];
        }

        public ISaveGame ModelObject { get; }

        public bool IsValid { get; }

        public ILocalizedText CombinedLabel { get; }

        public string WorldName { get; }

        public string MainPlayerName { get; }

        public int? MainPlayerLevel { get; }

        public int? DayNumber { get; }

        public ObservableCollection<ILocalizedText> Warnings { get; protected set; }
    }
}
