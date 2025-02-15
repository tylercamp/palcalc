using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel.Mapped;
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
        private SaveGameViewModel sourceSave;

        public PalTargetViewModel() : this(null, PalSpecifierViewModel.New, PassiveSkillsPresetCollectionViewModel.DesignerInstance) { }

        public PalTargetViewModel(SaveGameViewModel sourceSave, PalSpecifierViewModel initial, PassiveSkillsPresetCollectionViewModel presets)
        {
            this.sourceSave = sourceSave;

            if (initial.IsReadOnly)
            {
                InitialPalSpecifier = null;
                CurrentPalSpecifier = new PalSpecifierViewModel(null);

                PalSource = new PalSourceTreeViewModel(sourceSave.CachedValue);
            }
            else
            {
                InitialPalSpecifier = initial;
                CurrentPalSpecifier = initial.Copy();

                PalSource = new PalSourceTreeViewModel(sourceSave.CachedValue);
            }

            if (CurrentPalSpecifier.PalSourceId != null)
            {
                PalSource.SelectedNode = PalSource.FindById(CurrentPalSpecifier.PalSourceId) as IPalSourceTreeNode;
            }

            if (PalSource.SelectedSource != null)
                CurrentPalSpecifier.RefreshWith(AvailablePals);

            PropertyChangedEventManager.AddHandler(
                sourceSave.Customizations,
                (_, _) => CurrentPalSpecifier?.RefreshWith(AvailablePals),
                nameof(sourceSave.Customizations.CustomContainers)
            );

            PalSource.PropertyChanged += PalSource_PropertyChanged;
            
            Presets = presets;
            OpenPresetsMenuCommand = new RelayCommand(() => PresetsMenuIsOpen = true);

            presets.PresetSelected += (_) => PresetsMenuIsOpen = false;

            OpenPassivesSearchCommand = new RelayCommand(() => new PassivesSearchWindow() { Owner = App.Current.MainWindow }.Show());
        }

        private void PalSource_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalSource.HasValidSource))
                OnPropertyChanged(nameof(IsValid));

            if (e.PropertyName == nameof(PalSource.SelectedSource) && PalSource.SelectedSource != null)
            {
                CurrentPalSpecifier.PalSourceId = PalSource.SelectedSource.Id;
                CurrentPalSpecifier?.RefreshWith(AvailablePals);
            }
        }

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

                    if (value != null)
                    {
                        value?.RefreshWith(AvailablePals);
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

                case nameof(CurrentPalSpecifier.IncludeBasePals):
                case nameof(CurrentPalSpecifier.IncludeCagedPals):
                case nameof(CurrentPalSpecifier.IncludeCustomPals):
                    CurrentPalSpecifier.RefreshWith(AvailablePals);
                    break;
            }
        }

        public bool IsValid => PalSource.HasValidSource && CurrentPalSpecifier.IsValid;

        public PalSourceTreeViewModel PalSource { get; set; }

        public PassiveSkillsPresetCollectionViewModel Presets { get; }

        public IEnumerable<PalInstance> AvailablePals
        {
            get
            {
                if (PalSource?.SelectedSource != null)
                {
                    foreach (var pal in PalSource.SelectedSource.Filter(sourceSave.CachedValue))
                    {
                        if (!CurrentPalSpecifier.IncludeBasePals && pal.Location.Type == LocationType.Base)
                            continue;

                        if (!CurrentPalSpecifier.IncludeCagedPals && pal.Location.Type == LocationType.ViewingCage)
                            continue;

                        yield return pal;
                    }
                }

                if (CurrentPalSpecifier.IncludeCustomPals)
                {
                    foreach (var pal in sourceSave.Customizations.CustomContainers.SelectMany(c => c.Contents))
                        if (pal.IsValid)
                            yield return pal.ModelObject;
                }
            }
        }

        [ObservableProperty]
        private bool presetsMenuIsOpen = false;

        public IRelayCommand OpenPresetsMenuCommand { get; }

        public IRelayCommand OpenPassivesSearchCommand { get; }
    }
}
