using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel.Inspector.Search;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PalCalc.UI.ViewModel.Inspector
{
    public partial class SearchViewModel : ObservableObject
    {
        private static SearchViewModel designerInstance = null;
        public static SearchViewModel DesignerInstance => designerInstance ??= new SearchViewModel(SaveGameViewModel.DesignerInstance);

        [ObservableProperty]
        private OwnerTreeViewModel ownerTree;

        public SearchSettingsViewModel SearchSettings { get; }

        public ICommand NewCustomContainerCommand { get; }

        public SearchViewModel(SaveGameViewModel sgvm)
        {
            BuildContainerTree(sgvm);
            SearchSettings = new SearchSettingsViewModel();

            OwnerTree.PropertyChanging += OwnerTree_PropertyChanging;
            OwnerTree.PropertyChanged += OwnerTree_PropertyChanged;
            SearchSettings.PropertyChanged += SearchSettings_PropertyChanged;

            NewCustomContainerCommand = new RelayCommand(() =>
            {
                var nameModal = new SimpleTextInputWindow()
                {
                    Title = "New Custom Container",
                    InputLabel = "Name",
                    // TODO - prevent duplicate names
                    Validator = name => name.Length > 0
                };
                nameModal.Owner = App.ActiveWindow;
                if (nameModal.ShowDialog() == true)
                {
                    var container = new CustomContainer() { Label = nameModal.Result };
                    sgvm.Customizations.CustomContainers.Add(new CustomContainerViewModel(container));

                    BuildContainerTree(sgvm);
                }
            });
        }

        private void BuildContainerTree(SaveGameViewModel sgvm)
        {
            var csg = sgvm.CachedValue;
            var palsByContainerId = csg.OwnedPals.GroupBy(p => p.Location.ContainerId).ToDictionary(g => g.Key, g => g.ToList());

            var containers = palsByContainerId
                .Select(kvp => (ISearchableContainerViewModel)new DefaultSearchableContainerViewModel(kvp.Key, kvp.Value.First().Location.Type, kvp.Value))
                .Concat(sgvm.Customizations.CustomContainers.Select(c => new CustomSearchableContainerViewModel(c)));

            OwnerTree = new OwnerTreeViewModel(csg, containers.ToList());
        }

        private void ApplySearchSettings()
        {
            foreach (var source in OwnerTree.AllContainerSources)
                source.SearchCriteria = SearchSettings.AsCriteria;
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
            if (e.PropertyName == nameof(DefaultSearchableContainerViewModel.SelectedSlot))
            {
                OnPropertyChanged(nameof(SlotDetailsVisibility));
            }
        }
    }
}
