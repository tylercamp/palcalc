using CommunityToolkit.Mvvm.Input;
using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal record class SavesCollectionViewModel(
        SaveType SaveType,
        ReadOnlyObservableCollection<SaveGameViewModel2> AvailableSaves,
        ILocalizedText TypeLabel,
        ILocalizedText Title,
        IRelayCommand AddSaveCommand,
        IRelayCommand<SaveGameViewModel2> RemoveSaveCommand
    );
}
