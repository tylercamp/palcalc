using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    public partial class PalTargetViewModel : ObservableObject
    {
        public PalTargetViewModel() : this(null, PalSpecifierViewModel.New, PassiveSkillsPresetCollectionViewModel.DesignerInstance) { }

        public PalTargetViewModel(CachedSaveGame sourceSave, PalSpecifierViewModel initial, PassiveSkillsPresetCollectionViewModel presets)
        {
            if (initial.IsReadOnly)
            {
                InitialPalSpecifier = null;
                CurrentPalSpecifier = new PalSpecifierViewModel(null);

                PalSource = new PalSourceTreeViewModel(sourceSave);
            }
            else
            {
                InitialPalSpecifier = initial;
                CurrentPalSpecifier = initial.Copy();

                PalSource = new PalSourceTreeViewModel(sourceSave);
            }

            if (CurrentPalSpecifier.PalSourceId != null)
                PalSource.SelectedNode = PalSource.FindById(CurrentPalSpecifier.PalSourceId) as IPalSourceTreeNode;

            PalSource.PropertyChanged += PalSource_PropertyChanged;
            
            Presets = presets;
            OpenPresetsMenuCommand = new RelayCommand(() => PresetsMenuIsOpen = true);

            presets.PresetSelected += (_) => PresetsMenuIsOpen = false;
        }

        private void PalSource_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalSource.HasValidSource))
                OnPropertyChanged(nameof(IsValid));

            if (e.PropertyName == nameof(PalSource.SelectedSource) && PalSource.SelectedSource != null)
                CurrentPalSpecifier.PalSourceId = PalSource.SelectedSource.Id;
        }

        [ObservableProperty]
        private PalSpecifierViewModel initialPalSpecifier;

        private PalSpecifierViewModel currentPalSpecifier;
        public PalSpecifierViewModel CurrentPalSpecifier
        {
            get => currentPalSpecifier;
            set
            {
                var oldValue = CurrentPalSpecifier;
                if (SetProperty(ref currentPalSpecifier, value))
                {
                    if (oldValue != null) oldValue.PropertyChanged -= CurrentSpec_PropertyChanged;

                    value.PropertyChanged += CurrentSpec_PropertyChanged;
                    OnPropertyChanged(nameof(IsValid));
                }
            }
        }

        private void CurrentSpec_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurrentPalSpecifier.IsValid)) OnPropertyChanged(nameof(IsValid));
        }

        public List<PalViewModel> AvailablePals => AllPals;
        public List<PassiveSkillViewModel> AvailablePassives => AllPassives;

        public static List<PalViewModel> AllPals = PalDB.LoadEmbedded().Pals
            .OrderBy(p => p.Id)
            .Select(p => PalViewModel.Make(p))
            .ToList();

        public static List<PassiveSkillViewModel> AllPassives = PalDB.LoadEmbedded().PassiveSkills
            .DistinctBy(t => t.InternalName)
            .Select(PassiveSkillViewModel.Make)
            .OrderBy(t => t.Name.Value)
            .ToList();

        public bool IsValid => PalSource.HasValidSource && CurrentPalSpecifier.IsValid;

        public PalSourceTreeViewModel PalSource { get; set; }

        public PassiveSkillsPresetCollectionViewModel Presets { get; }

        [ObservableProperty]
        private bool presetsMenuIsOpen = false;

        public IRelayCommand OpenPresetsMenuCommand { get; }

        public void RefreshWith(CachedSaveGame csg)
        {
            CurrentPalSpecifier?.CurrentResults?.RefreshWith(csg);
        }
    }
}
