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

            DesignerInstance = new SaveSelectionOnboardingViewModel(locations, Enumerable.Empty<ISaveGame>());
        }

        public static SaveSelectionOnboardingViewModel DesignerInstance { get; }

        public IReadOnlyCollection<ISavesCollectionViewModel> AvailableSaveGameCollections { get; }

        [ObservableProperty]
        private ISavesCollectionViewModel selectedCollection;

        [ObservableProperty]
        private ISaveGameViewModel2 selectedSave;

        public SaveSelectionOnboardingViewModel(
            IEnumerable<ISavesLocation> savesLocations,
            IEnumerable<ISaveGame> manualSaves)
        {
            var steamCollections = savesLocations
                .OfType<DirectSavesLocation>()
                .Select(dsl => new SteamSavesCollectionViewModel(dsl))
                .Cast<ISavesCollectionViewModel>();

            if (!steamCollections.Any())
                steamCollections = [new SteamSavesCollectionViewModel(null)];

            var xboxCollections = savesLocations
                .OfType<XboxSavesLocation>()
                .Select(xsl => new XboxSavesCollectionViewModel(xsl))
                .Cast<ISavesCollectionViewModel>();

            var manualCollection = new ManualSavesCollectionViewModel(
                // TODO - simplify
                new ManualSavesLocationViewModel(manualSaves.OfType<StandardSaveGame>())
            );

            var fakeSavesCollection = new FakeSavesCollectionViewModel(
                manualSaves.OfType<VirtualSaveGame>()
            );

            AvailableSaveGameCollections = new ReadOnlyCollection<ISavesCollectionViewModel>(
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
