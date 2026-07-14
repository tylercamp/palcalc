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

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal class XboxSavesCollectionViewModel : ISavesCollectionViewModel
    {
        public XboxSavesCollectionViewModel(XboxSavesLocation savesLocation)
        {
            AvailableSaves = new ReadOnlyCollection<ISaveGameViewModel2>([.. savesLocation.AllSaveGames.OfType<XboxSaveGame>().Select(sg => new XboxSaveGameViewModel(sg))]);

            // TODO ITL
            TypeLabel = new HardCodedText("Xbox");
            Title = savesLocation.FolderPath != null ? new HardCodedText(((ISavesLocation)savesLocation).FolderName) : null;

            OpenFolderCommand = new RelayCommand(
                execute: () =>
                {
                    var fullPath = System.IO.Path.GetFullPath(savesLocation.FolderPath);
                    Process.Start("explorer.exe", fullPath);
                },
                canExecute: () => savesLocation?.FolderPath != null
            );
        }

        public IReadOnlyCollection<ISaveGameViewModel2> AvailableSaves { get; }

        
        public ILocalizedText TypeLabel { get; }

        public ILocalizedText Title { get; }

        public IRelayCommand OpenFolderCommand { get; }
    }
}
