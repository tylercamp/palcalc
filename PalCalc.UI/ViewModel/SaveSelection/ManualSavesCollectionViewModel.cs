using Microsoft.Xaml.Behaviors.Core;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
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
    internal class ManualSavesCollectionViewModel : ISavesCollectionViewModel
    {
        private ObservableCollection<ISaveGameViewModel2> _availableSaves;

        public ManualSavesCollectionViewModel(ManualSavesLocationViewModel location)
        {
            var removeSaveCommand = new ActionCommand((save) =>
            {

            });

            _availableSaves = new ([
                    .. location.SaveGames
                        .OfType<SaveGameViewModel>()
                        .Select(sg => sg.Value)
                        .OfType<StandardSaveGame>()
                        .Select(sg => new ManualSaveGameViewModel(sg))
                ]);
            AvailableSaves = new(_availableSaves);

            // TODO ITL
            TypeLabel = new HardCodedText("Local Files");
        }

        public ReadOnlyObservableCollection<ISaveGameViewModel2> AvailableSaves { get; }

        public ILocalizedText TypeLabel { get; }

        public ILocalizedText Title { get; } = null;

        public ICommand AddSaveCommand { get; }
    }
}
