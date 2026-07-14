using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    class ManualSaveGameViewModel : IStandardSaveGameViewModel
    {
        public ManualSaveGameViewModel(
            StandardSaveGame standardSave,
            IRelayCommand<ManualSaveGameViewModel> removeSaveCommand
        ) : base(standardSave)
        {
            RemoveSaveCommand = removeSaveCommand;
        }
        public IRelayCommand<ManualSaveGameViewModel> RemoveSaveCommand { get; }
    }
}
