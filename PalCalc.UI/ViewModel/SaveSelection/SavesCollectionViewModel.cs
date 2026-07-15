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
    public class SavesCollectionViewModel
    {
        public ISavesLocation SourceLocation { get; set; }
        public SaveType SaveType { get; set; }
        public ReadOnlyObservableCollection<SaveGameViewModel2> AvailableSaves { get; set; }
        public ILocalizedText TypeLabel { get; set; }
        public ILocalizedText Title { get; set; }
        public IRelayCommand OpenFolderCommand { get; set; }
        public IRelayCommand AddSaveCommand { get; set; }
        public IRelayCommand<SaveGameViewModel2> RemoveSaveCommand { get; set; }
    }
}
