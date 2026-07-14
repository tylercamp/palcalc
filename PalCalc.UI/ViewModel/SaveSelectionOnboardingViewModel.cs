using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.SaveSelection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    internal partial class SaveSelectionOnboardingViewModel : ObservableObject
    {
        // TODO - Right now Steam is completely hidden if not detected, should instead display with
        //        warning "Steam saves location not found"

        static SaveSelectionOnboardingViewModel()
        {
            List<ISavesLocation> locations = DirectSavesLocation.AllLocal.Cast<ISavesLocation>().ToList();
            var xboxLocations = XboxSavesLocation.FindAll();
            if (xboxLocations.Count > 0)
                locations.AddRange(xboxLocations);
            else
                locations.Add(new XboxSavesLocation());

            DesignerInstance = new SaveSelectionOnboardingViewModel(locations, Enumerable.Empty<ISaveGame>(), null);
        }

        public static SaveSelectionOnboardingViewModel DesignerInstance { get; }

        public IReadOnlyCollection<SavesCollectionViewModel> AvailableSaveGameCollections { get; }

        [ObservableProperty]
        private SavesCollectionViewModel selectedCollection;

        [ObservableProperty]
        private SaveGameViewModel2 selectedSave;

        public SaveSelectionOnboardingViewModel(
            IEnumerable<ISavesLocation> savesLocations,
            IEnumerable<ISaveGame> manualSaves,
            ISavesService savesService
        )
        {
            var steamCollections = SteamSaves.CollectAll(savesLocations, savesService);

            if (!steamCollections.Any())
                steamCollections = [SteamSaves.MakeEmptyCollection()];

            var xboxCollections = XboxSaves.CollectAll(savesLocations);

            var manualCollection = ManualSaves.FromList(manualSaves.OfType<StandardSaveGame>(), savesService);

            var fakeSavesCollection = VirtualSaves.FromList(manualSaves.OfType<VirtualSaveGame>(), savesService); ;

            AvailableSaveGameCollections = new ReadOnlyCollection<SavesCollectionViewModel>(
                [ ..steamCollections, ..xboxCollections, manualCollection, fakeSavesCollection ]
            );
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedCollection))
            {
                SelectedSave = null;
            }

            base.OnPropertyChanged(e);
        }
    }
}
