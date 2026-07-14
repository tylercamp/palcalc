using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    class FakeSaveGameViewModel : ISaveGameViewModel2, IRemovableSaveGameViewModel
    {
        public FakeSaveGameViewModel(VirtualSaveGame vsg, ICommand removeSaveCommand)
        {
            ModelObject = vsg;

            var meta = vsg.LevelMeta.ReadGameOptions();
            CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(
                // (InGameDay is always default 0, but the format param is required for this translation code)
                new { WorldName = meta.WorldName, DayNumber = meta.InGameDay }
            );

            WorldName = meta.WorldName;
            DayNumber = meta.InGameDay;

            MainPlayerName = null;
            MainPlayerLevel = null;

            Warnings = [];

            RemoveSaveCommand = removeSaveCommand;
        }

        public ISaveGame ModelObject { get; }

        public bool IsValid => true;

        public ILocalizedText CombinedLabel { get; }

        public string WorldName { get; }

        public string MainPlayerName { get; }

        public int? MainPlayerLevel { get; }

        public int? DayNumber { get; }

        public ObservableCollection<ILocalizedText> Warnings { get; }

        public ICommand RemoveSaveCommand { get; }
    }
}
