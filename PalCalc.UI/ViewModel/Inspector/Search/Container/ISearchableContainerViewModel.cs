﻿using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Inspector.Search.Grid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search.Container
{
    public abstract class ISearchableContainerViewModel(GameSettings settings) : ObservableObject
    {
        public abstract string Id { get; }
        public abstract LocationType DetectedType { get; }

        public int RowSize => settings.LocationTypeGridWidths[DetectedType];

        public abstract List<IContainerGridViewModel> Grids { get; }

        public ISearchCriteria SearchCriteria
        {
            set
            {
                foreach (var grid in Grids)
                    grid.SearchCriteria = value;
            }
        }

        public IContainerGridSlotViewModel SelectedSlot => Grids.FirstOrDefault(g => g.SelectedSlot != null)?.SelectedSlot;
        public IContainerGridPopulatedSlotViewModel SelectedPalSlot => SelectedSlot as IContainerGridPopulatedSlotViewModel;

        // if a slot is selected in the grid, raise a matching event here
        // and, if a value was selected in one grid, deselect values in all other grids
        private bool isSyncingSlots = false;
        protected void OnSyncGridSelectedSlot(object sender, PropertyChangedEventArgs e)
        {
            if (isSyncingSlots || e.PropertyName != nameof(IContainerGridViewModel.SelectedSlot)) return;

            isSyncingSlots = true;
            var srcGrid = sender as IContainerGridViewModel;

            foreach (var grid in Grids)
            {
                if (grid == srcGrid)
                    continue;

                grid.SelectedSlot = null;
            }

            isSyncingSlots = false;

            OnPropertyChanged(nameof(SelectedSlot));
            OnPropertyChanged(nameof(SelectedPalSlot));
        }
    }
}
