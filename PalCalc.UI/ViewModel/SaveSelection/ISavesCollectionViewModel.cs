using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal interface ISavesCollectionViewModel
    {
        ReadOnlyObservableCollection<ISaveGameViewModel2> AvailableSaves { get; }

        ILocalizedText TypeLabel { get; }

        ILocalizedText Title { get; }

        IRelayCommand AddSaveCommand { get; }
        IRelayCommand<ISaveGameViewModel2> RemoveSaveCommand { get; }
    }
}
