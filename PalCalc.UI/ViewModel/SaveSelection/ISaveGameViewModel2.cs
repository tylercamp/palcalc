using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    interface ISaveGameViewModel2
    {
        ISaveGame ModelObject { get; }
        bool IsValid { get; }

        ILocalizedText CombinedLabel { get; }

        public string WorldName { get; }
        public string MainPlayerName { get; }
        public int? MainPlayerLevel { get; }
        public int? DayNumber { get; }

        ObservableCollection<ILocalizedText> Warnings { get; }
    }
}
