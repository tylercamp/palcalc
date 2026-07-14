using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using Serilog;
using Serilog.Core;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal static class SavesCommon
    {
        private static ILogger logger = Log.ForContext(typeof(SavesCommon));

        public static SaveGameViewModel2 BuildNormalSave(ISaveGame save, IRelayCommand openFolderCommand)
        {
            var res = new SaveGameViewModel2()
            {
                ModelObject = save,
                IsValid = save.IsValid,

                OpenFolderCommand = openFolderCommand
            };

            try
            {
                var meta = save.LevelMeta.ReadGameOptions();
                res.WorldName = meta.WorldName;
                res.DayNumber = meta.InGameDay;

                if (meta.IsServerSave)
                {
                    res.CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(new
                    {
                        DayNumber = meta.InGameDay,
                        WorldName = meta.WorldName,
                    });
                }
                else
                {
                    res.MainPlayerName = meta.PlayerName;
                    res.MainPlayerLevel = meta.PlayerLevel;

                    res.CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL.Bind(new
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
                logger.Warning(ex, "error when loading LevelMeta for {saveId}", CachedSaveGame.IdentifierFor(save));
                res.CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_NO_METADATA.Bind(save.GameId);
            }

            return res;
        }
    }
}
