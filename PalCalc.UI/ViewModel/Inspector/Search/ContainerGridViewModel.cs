﻿using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public interface IContainerGridSlotViewModel {
        bool Matches { get; }
    }

    public partial class ContainerGridPalSlotViewModel : ObservableObject, IContainerGridSlotViewModel
    {
        public PalInstanceViewModel PalInstance { get; set; }

        public PalViewModel Pal => PalInstance.Pal;

        [ObservableProperty]
        private bool matches = true;
    }

    public class ContainerGridEmptySlotViewModel : IContainerGridSlotViewModel
    {
        public bool Matches => false;
    }

    public partial class ContainerGridViewModel(List<PalInstance> contents) : ObservableObject
    {
        private static ContainerGridViewModel designerInstance;
        public static ContainerGridViewModel DesignerInstance
        {
            get
            {
                if (designerInstance == null)
                {
                    var c = CachedSaveGame.SampleForDesignerView.OwnedPals.GroupBy(p => p.Location.ContainerId).First();

                    designerInstance = new ContainerGridViewModel(c.ToList())
                    {
                        Title = new HardCodedText("Tab 1"),
                        PerRow = 5
                    };
                }
                return designerInstance;
            }
        }

        [ObservableProperty]
        private int perRow = 5;

        public List<PalInstance> Contents => contents;

        public ISearchCriteria SearchCriteria
        {
            set
            {
                foreach (var slot in Slots.Where(s => s is ContainerGridPalSlotViewModel).Cast<ContainerGridPalSlotViewModel>())
                    slot.Matches = value.Matches(slot.PalInstance.ModelObject);

                if (SelectedSlot != null && !SelectedSlot.Matches)
                    SelectedSlot = null;

                OnPropertyChanged(nameof(GridVisibility));
            }
        }

        public Visibility GridVisibility => Title == null || Slots.Any(s => s is ContainerGridPalSlotViewModel { Matches: true }) ? Visibility.Visible : Visibility.Collapsed;

        public ILocalizedText Title { get; set; }
        public Visibility TitleVisibility => Title == null ? Visibility.Collapsed : Visibility.Visible;

        [ObservableProperty]
        private IContainerGridSlotViewModel selectedSlot;

        public List<IContainerGridSlotViewModel> Slots { get; } = contents == null ? [] :
            contents
                .Select<PalInstance, IContainerGridSlotViewModel>(p =>
                {
                    if (p == null) return new ContainerGridEmptySlotViewModel();
                    else return new ContainerGridPalSlotViewModel() { PalInstance = new PalInstanceViewModel(p), Matches = true };
                })
                .ToList();
    }
}
