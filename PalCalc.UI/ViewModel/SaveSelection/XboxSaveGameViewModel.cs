using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    class XboxSaveGameViewModel : IStandardSaveGameViewModel
    {
        public XboxSaveGameViewModel(XboxSaveGame xboxSave) : base(xboxSave)
        {
            if (xboxSave.LevelMeta?.IsValid != true)
            {
                Warnings = [
                    LocalizationCodes.LC_SAVE_GAME_XBOX_INCOMPLETE.Bind(),
                ];
            }
        }
    }
}
