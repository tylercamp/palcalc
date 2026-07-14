using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    class SteamSaveGameViewModel : IStandardSaveGameViewModel
    {
        public SteamSaveGameViewModel(StandardSaveGame standardSave) : base(standardSave)
        {
        }
    }
}
