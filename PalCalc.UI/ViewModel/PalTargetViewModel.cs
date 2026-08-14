using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Presets;
using PalCalc.UI.ViewModel.SaveSelection;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace PalCalc.UI.ViewModel
{
    /// <summary>
    /// View-model for the pal target settings on the right side of the main window. Manages the pal specifier
    /// pal sources, presets, etc.
    /// 
    /// This is more of a mediator than a proper view-wmodel object and isn't involved in serialization.
    /// </summary>
    public partial class PalTargetViewModel : ObservableObject
    {
        private PalSourceViewModel sourcePals;

        public PalTargetViewModel() : this(null, null, PalSpecifierViewModel.New, PassiveSkillsPresetCollectionViewModel.DesignerInstance) { }

        public PalTargetViewModel(SaveGameViewModel sourceSave, PalSourceViewModel sourcePals, PalSpecifierViewModel initial, PassiveSkillsPresetCollectionViewModel presets)
        {
            this.sourcePals = sourcePals;

            if (initial.IsReadOnly)
            {
                InitialPalSpecifier = null;
                CurrentPalSpecifier = new PalSpecifierViewModel(Guid.NewGuid().ToString(), null);
            }
            else
            {
                InitialPalSpecifier = initial;
                if (initial.LatestJob != null && initial.LatestJob.Results == null && initial.LatestJob.CurrentState != SolverState.Idle)
                {
                    CurrentPalSpecifier = initial.LatestJob.Specifier;
                }
                else
                {
                    CurrentPalSpecifier = initial.Copy();
                }
            }

            CurrentPalSpecifier.RefreshWith(sourcePals.AvailablePals);

            void RefreshOnChange(object sender, PropertyChangedEventArgs ev)
            {
                CurrentPalSpecifier?.RefreshWith(sourcePals.AvailablePals);
            }

            PropertyChangedEventManager.AddHandler(sourceSave.Customizations, RefreshOnChange, nameof(sourceSave.Customizations.CustomContainers));
            PropertyChangedEventManager.AddHandler(sourcePals, RefreshOnChange, nameof(sourcePals.AvailablePals));
            
            Presets = presets;
            OpenPresetsMenuCommand = new RelayCommand(() => PresetsMenuIsOpen = true);

            presets.PresetSelected += (_) => PresetsMenuIsOpen = false;

            OpenPassivesSearchCommand = new RelayCommand(() => new PassivesSearchWindow() { Owner = App.Current.MainWindow }.Show());
        }

        private SolverJobViewModel currentLatestJob;
        public SolverJobViewModel CurrentLatestJob
        {
            get => currentLatestJob;
            private set
            {
                if (currentLatestJob != null && currentLatestJob != value)
                {
                    currentLatestJob.PropertyChanged -= CurrentLatestJob_PropertyChanged;
                }

                if (SetProperty(ref currentLatestJob, value))
                {
                    OnPropertyChanged(nameof(CanEdit));

                    if (value != null)
                    {
                        value.PropertyChanged += CurrentLatestJob_PropertyChanged;
                    }
                }
            }
        }

        private void CurrentLatestJob_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentLatestJob.IsActive))
                OnPropertyChanged(nameof(CanEdit));
        }

        public bool CanEdit => CurrentLatestJob == null || !CurrentLatestJob.IsActive;

        [ObservableProperty]
        private PalSpecifierViewModel initialPalSpecifier;

        private PalSpecifierViewModel currentPalSpecifier;
        public PalSpecifierViewModel CurrentPalSpecifier
        {
            get => currentPalSpecifier;
            private set
            {
                var oldValue = CurrentPalSpecifier;
                if (SetProperty(ref currentPalSpecifier, value))
                {
                    if (oldValue != null) oldValue.PropertyChanged -= CurrentSpec_PropertyChanged;

                    value.PropertyChanged += CurrentSpec_PropertyChanged;
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(CanEdit));

                    CurrentLatestJob = value?.LatestJob;

                    if (value != null)
                    {
                        value?.RefreshWith(sourcePals.AvailablePals);
                    }
                }
            }
        }

        private void CurrentSpec_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CurrentPalSpecifier.IsValid):
                    OnPropertyChanged(nameof(IsValid));
                    break;

                case nameof(CurrentPalSpecifier.LatestJob):
                    CurrentLatestJob = CurrentPalSpecifier.LatestJob;
                    break;
            }
        }

        public bool IsValid => CurrentPalSpecifier.IsValid;

        public PassiveSkillsPresetCollectionViewModel Presets { get; }

        [ObservableProperty]
        private bool presetsMenuIsOpen = false;

        public IRelayCommand OpenPresetsMenuCommand { get; }

        public IRelayCommand OpenPassivesSearchCommand { get; }
    }
}
