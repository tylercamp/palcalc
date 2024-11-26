using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.View.Utils;
using PalCalc.UI.ViewModel.Inspector.Search;
using PalCalc.UI.ViewModel.Inspector.Search.Container;
using PalCalc.UI.ViewModel.Inspector.Search.Grid;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph.Graphviz;
using QuickGraph.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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

        private ICommand newCustomContainerCommand;
        public IRelayCommand<ISearchableContainerViewModel> DeleteContainerCommand { get; }

        public IRelayCommand<ISearchableContainerViewModel> RenameContainerCommand { get; }

        public IRelayCommand<IContainerGridSlotViewModel> DeleteSlotCommand { get; }

        private static bool IsValidCustomLabel(SaveGameViewModel context, string label) =>
            label.Length > 0 && !context.Customizations.CustomContainers.Any(c => c.Label == label);

        public SearchViewModel(SaveGameViewModel sgvm)
        {
            newCustomContainerCommand = new RelayCommand(() =>
            {
                var nameModal = new SimpleTextInputWindow()
                {
                    Title = LocalizationCodes.LC_CUSTOM_CONTAINER_NEW_TITLE.Bind().Value,
                    InputLabel = LocalizationCodes.LC_CUSTOM_CONTAINER_NEW_FIELD.Bind().Value,
                    Validator = label => IsValidCustomLabel(sgvm, label),
                    Owner = App.ActiveWindow,
                };

                if (nameModal.ShowDialog() == true)
                {
                    var container = new CustomContainer() { Label = nameModal.Result };
                    sgvm.Customizations.CustomContainers.Add(new CustomContainerViewModel(container));
                }
            });

            RenameContainerCommand = new RelayCommand<ISearchableContainerViewModel>(
                container =>
                {
                    var customContainer = container as CustomSearchableContainerViewModel;
                    if (customContainer == null) return;

                    var nameModal = new SimpleTextInputWindow(customContainer.Label)
                    {
                        Title = LocalizationCodes.LC_CUSTOM_CONTAINER_RENAME_TITLE.Bind().Value,
                        InputLabel = LocalizationCodes.LC_CUSTOM_CONTAINER_RENAME_FIELD.Bind().Value,
                        Validator = label => IsValidCustomLabel(sgvm, label),
                        Owner = App.ActiveWindow,
                    };

                    if (nameModal.ShowDialog() == true)
                    {
                        customContainer.Value.Label = nameModal.Result;
                    }
                }
            );

            DeleteContainerCommand = new RelayCommand<ISearchableContainerViewModel>(
                container =>
                {
                    var customContainer = container as CustomSearchableContainerViewModel;
                    if (customContainer == null) return;

                    if (MessageBox.Show(LocalizationCodes.LC_REMOVE_CUSTOM_CONTAINER_DESCRIPTION.Bind(customContainer.Label).Value, "", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                        return;

                    sgvm.Customizations.CustomContainers.Remove(customContainer.Value);
                }
            );

            DeleteSlotCommand = new RelayCommand<IContainerGridSlotViewModel>(
                slot =>
                {
                    var subCommands = OwnerTree.AllContainerSources
                        .SelectMany(s => s.Container.Grids)
                        .Select(g => g.DeleteSlotCommand)
                        .SkipNull()
                        .Where(cmd => cmd.CanExecute(slot))
                        .ToList();

                    foreach (var cmd in subCommands)
                        cmd.Execute(slot);
                }
            );

            CollectionChangedEventManager.AddHandler(
                sgvm.Customizations.CustomContainers,
                (_, _) => BuildContainerTree(sgvm)
            );

            BuildContainerTree(sgvm);
            SearchSettings = new SearchSettingsViewModel();

            SearchSettings.PropertyChanged += SearchSettings_PropertyChanged;
        }

        private void BuildContainerTree(SaveGameViewModel sgvm)
        {
            var csg = sgvm.CachedValue;
            var palsByContainerId = csg.OwnedPals.GroupBy(p => p.Location.ContainerId).ToDictionary(g => g.Key, g => g.ToList());

            var containers = csg.PalContainers
                .Where(c => palsByContainerId.ContainsKey(c.Id))
                .Select(c => new DefaultSearchableContainerViewModel(c, palsByContainerId[c.Id]))
                .Cast<ISearchableContainerViewModel>()
                .Concat(
                    sgvm.Customizations.CustomContainers.Select(c =>
                        new CustomSearchableContainerViewModel(c)
                        {
                            RenameCommand = RenameContainerCommand,
                            DeleteCommand = DeleteContainerCommand,
                        }
                    )
                );

            OwnerTree = new OwnerTreeViewModel(csg, containers.ToList())
            {
                CreateCustomContainerCommand = newCustomContainerCommand
            };
        }

        private void ApplySearchSettings()
        {
            foreach (var source in OwnerTree.AllContainerSources)
                source.SearchCriteria = SearchSettings.AsCriteria;
        }

        private void SearchSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchSettings.AsCriteria))
            {
                ApplySearchSettings();
            }
        }

    }
}
