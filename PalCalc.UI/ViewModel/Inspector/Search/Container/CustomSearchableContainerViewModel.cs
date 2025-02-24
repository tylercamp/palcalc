using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Inspector.Search.Grid;
using PalCalc.UI.ViewModel.PalDerived;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search.Container
{
    public partial class CustomSearchableContainerViewModel : ISearchableContainerViewModel
    {
        private CustomContainerViewModel container;
        public CustomSearchableContainerViewModel(GameSettings settings, CustomContainerViewModel container) : base(settings)
        {
            this.container = container;

            PropertyChangedEventManager.AddHandler(
                container,
                (_, _) =>
                {
                    OnPropertyChanged(nameof(Label));
                    OnPropertyChanged(nameof(Id));
                },
                nameof(container.Label)
            );
        }

        // (these are provided by the caller, ContextMenu bound to this VM isn't able to easily
        //  traverse visual tree to find the main VM)
        public IRelayCommand<ISearchableContainerViewModel> RenameCommand { get; set; }
        public IRelayCommand<ISearchableContainerViewModel> DeleteCommand { get; set; }

        public string Label => container.Label;

        public override string Id => container.Label;

        public override LocationType DetectedType => container.LocationType;

        public CustomContainerViewModel Value => container;

        private List<IContainerGridViewModel> grids;
        public override List<IContainerGridViewModel> Grids
        {
            get
            {
                if (grids == null)
                {
                    grids = [new CustomContainerGridViewModel(container) { RowSize = RowSize }];

                    foreach (var grid in grids)
                    {
                        grid.PropertyChanged += OnSyncGridSelectedSlot;
                    }
                }
                return grids;
            }
        }
    }
}
