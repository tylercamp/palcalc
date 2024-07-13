using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Inspector.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.Inspector
{
    public partial class SearchViewModel : ObservableObject
    {
        private static SearchViewModel designerInstance = null;
        public static SearchViewModel DesignerInstance => designerInstance ??= new SearchViewModel(CachedSaveGame.SampleForDesignerView);

        public OwnerTreeViewModel OwnerTree { get; }
        public SearchSettingsViewModel SearchSettings { get; }

        public SearchViewModel(CachedSaveGame csg)
        {
            var palsByContainerId = csg.OwnedPals.GroupBy(p => p.Location.ContainerId).ToDictionary(g => g.Key, g => g.ToList());

            var containers = palsByContainerId.Select(kvp => new ContainerViewModel(kvp.Key, kvp.Value.First().Location.Type, kvp.Value));

            OwnerTree = new OwnerTreeViewModel(csg, containers.ToList());
            SearchSettings = new SearchSettingsViewModel();

            OwnerTree.PropertyChanging += OwnerTree_PropertyChanging;
            OwnerTree.PropertyChanged += OwnerTree_PropertyChanged;
            SearchSettings.PropertyChanged += SearchSettings_PropertyChanged;
        }

        private void ApplySearchSettings()
        {
            foreach (var source in OwnerTree.AllContainerSources)
                source.SearchCriteria = SearchSettings.AsCriteria;

            //OwnerTree.SelectedSource.SearchCriteria = SearchSettings.AsCriteria;
            //foreach (var grid in OwnerTree.SelectedSource.Container.Grids)
            //    grid.SearchCriteria = SearchSettings.AsCriteria;
        }

        private void OwnerTree_PropertyChanging(object sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            if (e.PropertyName == nameof(OwnerTree.SelectedSource))
            {
                if (OwnerTree.SelectedSource != null)
                    OwnerTree.SelectedSource.Container.PropertyChanged -= SelectedContainer_PropertyChanged;
            }
        }

        private void OwnerTree_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OwnerTree.SelectedSource))
            {
                OnPropertyChanged(nameof(SlotDetailsVisibility));

                if (OwnerTree.SelectedSource != null)
                {
                    OwnerTree.SelectedSource.Container.PropertyChanged += SelectedContainer_PropertyChanged;
                }
            }
        }

        private void SearchSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchSettings.AsCriteria))
            {
                ApplySearchSettings();
            }
        }

        public Visibility SlotDetailsVisibility => OwnerTree.HasValidSource && OwnerTree.SelectedSource.Container.SelectedSlot != null ? Visibility.Visible : Visibility.Collapsed;

        private void SelectedContainer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ContainerViewModel.SelectedSlot))
            {
                OnPropertyChanged(nameof(SlotDetailsVisibility));
            }
        }
    }
}
