using CommunityToolkit.Mvvm.Input;
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
    internal class SaveGameViewModel2
    {
        public ISaveGame ModelObject { get; set; }
        public bool IsValid { get; set; }

        public ILocalizedText CombinedLabel { get; set; }

        public ObservableCollection<ILocalizedText> Warnings { get; set; }

        public string WorldName { get; set; }
        public string MainPlayerName { get; set; }
        public int? MainPlayerLevel { get; set; }
        public int? DayNumber { get; set; }

        public IRelayCommand OpenFolderCommand { get; set; }
    }
}
