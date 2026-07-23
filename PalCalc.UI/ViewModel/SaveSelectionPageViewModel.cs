using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Mapped.Saves;
using PalCalc.UI.ViewModel.SaveSelection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    internal partial class SaveSelectionPageViewModel : ObservableObject
    {
        private static SaveSelectionPageViewModel _designerInstance;
        public static SaveSelectionPageViewModel DesignerInstance =>
            _designerInstance ??= new SaveSelectionPageViewModel(SavesCollectionViewModel.DetectAll(new AppSettings(), null), null);

        public IReadOnlyCollection<SavesCollectionViewModel> AvailableSaveGameCollections { get; }

        public IRelayCommand<SaveGameViewModel> LoadSaveCommand { get; }

        [ObservableProperty]
        private SavesCollectionViewModel selectedCollection;

        [ObservableProperty]
        private SaveGameViewModel selectedSave;

        [ObservableProperty]
        private CommonSaveOperationsViewModel commonSelectedSaveOperations;

        [ObservableProperty]
        private bool isLocationHelpMenuOpen;

        public SaveSelectionPageViewModel(
            IEnumerable<SavesCollectionViewModel> savesCollections,
            IRelayCommand<SaveGameViewModel> loadSaveCommand
        )
        {
            AvailableSaveGameCollections = [.. savesCollections];
            LoadSaveCommand = loadSaveCommand;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectedCollection))
            {
                SelectedSave = null;
            }

            if (e.PropertyName == nameof(SelectedSave))
            {
                // (`null` for `navigateSaveSelectionPageCommand` since we're already on that page)
                CommonSelectedSaveOperations = new CommonSaveOperationsViewModel(null, SelectedCollection, SelectedSave);
            }

            base.OnPropertyChanged(e);
        }

        public void TrySelectSaveByIdentifier(string id)
        {
            foreach (var loc in AvailableSaveGameCollections)
            {
                foreach (var save in loc.AvailableSaves)
                {
                    if (CachedSaveGame.IdentifierFor(save.Value) == id)
                    {
                        SelectedCollection = loc;
                        SelectedSave = save;
                        break;
                    }
                }
            }
        }

        [RelayCommand]
        private void OpenLocationHelpMenu()
        {
            IsLocationHelpMenuOpen = true;
        }
    }
}
