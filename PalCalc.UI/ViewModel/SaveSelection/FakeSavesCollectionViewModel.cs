using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal class FakeSavesCollectionViewModel : ISavesCollectionViewModel
    {
        private ObservableCollection<ISaveGameViewModel2> _availableSaves;

        public FakeSavesCollectionViewModel(IEnumerable<VirtualSaveGame> fakeSaves)
        {
            _availableSaves = new([.. fakeSaves.Select(fs => new FakeSaveGameViewModel(fs))]);
            AvailableSaves = new(_availableSaves);

            // TODO ITL
            TypeLabel = new HardCodedText("Fake Saves");
            Title = null;
        }

        public ReadOnlyObservableCollection<ISaveGameViewModel2> AvailableSaves { get; }

        public ILocalizedText TypeLabel { get; }

        public ILocalizedText Title { get; }

        public ICommand AddSaveCommand { get; }
    }
}
