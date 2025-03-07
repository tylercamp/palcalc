﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.Inspector.Search.Grid
{
    public partial class ContainerGridPalSlotViewModel : ObservableObject, IContainerGridPopulatedSlotViewModel
    {
        public PalInstanceViewModel PalInstance { get; set; }

        public PalViewModel Pal => PalInstance.Pal;

        [NotifyPropertyChangedFor(nameof(CanInterract))]
        [ObservableProperty]
        private bool matches = true;

        public bool CanInterract => Matches;
    }

    public class ContainerGridEmptySlotViewModel : IContainerGridSlotViewModel
    {
        public bool Matches => false;
        public bool CanInterract => false;
    }

    public partial class DefaultContainerGridViewModel(List<PalInstance> contents) : ObservableObject, IContainerGridViewModel
    {
        private static DefaultContainerGridViewModel designerInstance;
        public static DefaultContainerGridViewModel DesignerInstance
        {
            get
            {
                if (designerInstance == null)
                {
                    var c = CachedSaveGame.SampleForDesignerView.OwnedPals.GroupBy(p => p.Location.ContainerId).First();

                    designerInstance = new DefaultContainerGridViewModel(c.Take(20).ToList())
                    {
                        Title = new HardCodedText("Tab 1"),
                        RowSize = 5
                    };
                }
                return designerInstance;
            }
        }

        [ObservableProperty]
        private int rowSize = 5;

        public List<PalInstance> Contents => contents;

        public ISearchCriteria SearchCriteria
        {
            set
            {
                foreach (var slot in Slots.OfType<ContainerGridPalSlotViewModel>())
                    slot.Matches = value.Matches(slot.PalInstance.ModelObject);

                if (SelectedSlot != null && !SelectedSlot.Matches)
                    SelectedSlot = null;

                OnPropertyChanged(nameof(GridVisibility));
            }
        }

        public Visibility GridVisibility => Title == null || Slots.Any(s => s is ContainerGridPalSlotViewModel { Matches: true }) ? Visibility.Visible : Visibility.Collapsed;

        public ILocalizedText Title { get; set; }

        public IRelayCommand<IContainerGridSlotViewModel> DeleteSlotCommand => null;

        [ObservableProperty]
        private IContainerGridSlotViewModel selectedSlot;

        public ObservableCollection<IContainerGridSlotViewModel> Slots { get; } = contents == null ? [] : new ObservableCollection<IContainerGridSlotViewModel>(
            contents
                .Select<PalInstance, IContainerGridSlotViewModel>(p =>
                {
                    if (p == null) return new ContainerGridEmptySlotViewModel();
                    else return new ContainerGridPalSlotViewModel() { PalInstance = new PalInstanceViewModel(p), Matches = true };
                })
        );
    }
}
