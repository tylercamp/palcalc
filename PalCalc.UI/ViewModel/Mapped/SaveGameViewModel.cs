using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.Mapped
{
    public partial class SaveGameViewModel
    {
        private static ILogger logger = Log.ForContext<SaveGameViewModel>();

        private SaveGameViewModel()
        {
            IsAddManualOption = true;
            Label = Translator.Translations[LocalizationCodes.LC_ADD_NEW_SAVE].Bind();
        }

        public SaveGameViewModel(ISaveGame value)
        {
            IsAddManualOption = false;
            Value = value;

            try
            {
                var meta = value.LevelMeta.ReadGameOptions();
                if (meta.IsServerSave)
                {
                    Label = Translator.Translations[LocalizationCodes.LC_SAVE_GAME_LBL_SERVER].Bind(new()
                    {
                        { "DayNumber", meta.InGameDay },
                        { "WorldName", meta.WorldName }
                    });
                }
                else
                {
                    Label = Translator.Translations[LocalizationCodes.LC_SAVE_GAME_LBL].Bind(new()
                    {
                        { "PlayerName", meta.PlayerName },
                        { "PlayerLevel", meta.PlayerLevel },
                        { "DayNumber", meta.InGameDay },
                        { "WorldName", meta.WorldName },
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "error when loading LevelMeta for {saveId}", CachedSaveGame.IdentifierFor(value));
                Label = Translator.Translations[LocalizationCodes.LC_SAVE_GAME_LBL_NO_METADATA].Bind(
                    new (){ { "GameId", value.GameId } }
                );
            }

            IsValid = Value.IsValid;
        }

        public DateTime LastModified => Value.LastModified;

        public ISaveGame Value { get; }
        public CachedSaveGame CachedValue => Storage.LoadSave(Value, PalDB.LoadEmbedded());
        public ILocalizedText Label { get; }

        public bool IsValid { get; }

        public bool IsAddManualOption { get; }

        public Visibility WarningVisibility => !IsAddManualOption && !IsValid ? Visibility.Visible : Visibility.Collapsed;

        public static readonly SaveGameViewModel AddNewSave = new SaveGameViewModel();
    }
}
