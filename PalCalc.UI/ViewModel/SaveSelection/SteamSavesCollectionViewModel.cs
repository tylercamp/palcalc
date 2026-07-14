using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    // (only Steam saves are currently supported for auto-detection as "Direct Saves")
    internal class SteamSavesCollectionViewModel : ISavesCollectionViewModel
    {
        public SteamSavesCollectionViewModel(DirectSavesLocation steamLocation)
        {
            AvailableSaves = steamLocation != null
                ? new([.. steamLocation.ValidSaveGames.OfType<StandardSaveGame>().Select(sg => new SteamSaveGameViewModel(sg))])
                : new([]);

            // TODO ITL
            TypeLabel = new HardCodedText("Steam");
            Title = new HardCodedText(steamLocation.FolderName);

            OpenFolderCommand = new RelayCommand(
                execute: () =>
                {
                    var fullPath = System.IO.Path.GetFullPath(steamLocation.FolderPath);
                    Process.Start("explorer.exe", fullPath);
                },
                canExecute: () => steamLocation?.FolderPath != null
            );
        }

        public ReadOnlyObservableCollection<ISaveGameViewModel2> AvailableSaves { get; }

        public ILocalizedText TypeLabel { get; }

        public ILocalizedText Title { get; }

        public IRelayCommand OpenFolderCommand { get; }
    }
}
