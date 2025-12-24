using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuzzySharp;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Presets;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    public partial class PalCheckListEntryViewModel : ObservableObject
    {
        private bool initialEnabled;
        public PalCheckListEntryViewModel(PalViewModel pal, bool initialEnabled)
        {
            Pal = pal;
            this.initialEnabled = initialEnabled;

            IsEnabled = initialEnabled;
        }

        public string PaldexNoDisplay => Pal.ModelObject.Id.PalDexNo.ToString() + (Pal.ModelObject.Id.IsVariant ? "B" : "");
        public double PaldexNoValue => Pal.ModelObject.Id.PalDexNo + (Pal.ModelObject.Id.IsVariant ? 0.1 : 0);
        public ILocalizedText PalName => Pal.Name;

        public PalViewModel Pal { get; }

        [NotifyPropertyChangedFor(nameof(HasChanges))]
        [ObservableProperty]
        private bool isEnabled;

        public bool HasChanges => IsEnabled != initialEnabled;
    }

    public partial class PalCheckListViewModel : ObservableObject
    {
        public IRelayCommand<object> SaveCommand { get; }

        public IRelayCommand<object> CancelCommand { get; }


        public static PalCheckListViewModel DesignerInstance { get; } =
            new PalCheckListViewModel(
                null, null, null,
                new Dictionary<Pal, bool>()
                {
                    { PalDB.LoadEmbedded().Pals.First(), true }
                }
            );

        public PalListPresetCollectionViewModel Presets { get; }

        private List<PalCheckListEntryViewModel> allEntries;

        [ObservableProperty]
        private List<PalCheckListEntryViewModel> visibleEntries;

        private string searchText = "";
        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value))
                {
                    if (value.Trim().Length > 0)
                    {
                        VisibleEntries = allEntries.Where(e => e.PalName.Value.Contains(value, StringComparison.CurrentCultureIgnoreCase) || Fuzz.PartialRatio(value.ToLower(), e.PalName.Value.ToLower()) > 80).ToList();
                    }
                    else
                    {
                        VisibleEntries = allEntries;
                    }
                }
            }
        }
        
        public PalCheckListViewModel(PalListPresetCollectionViewModel presets, Action onCancel, Action<Dictionary<Pal, bool>> onSave, Dictionary<Pal, bool> initialState)
        {
            allEntries = initialState
                .Select(kvp => new PalCheckListEntryViewModel(PalViewModel.Make(kvp.Key), kvp.Value))
                .OrderBy(vm => vm.Pal.ModelObject.Id)
                .ToList();

            VisibleEntries = allEntries;
            Presets = presets;

            if (Presets != null)
                Presets.ActivePalSelections = allEntries.Where(e => e.IsEnabled).Select(e => e.Pal.ModelObject).ToList();

            foreach (var e in allEntries)
                e.PropertyChanged += EntryPropertyChanged;

            SaveCommand = new RelayCommand<object>(
                execute: (window) =>
                {
                    DetachEvents();
                    onSave?.Invoke(allEntries.ToDictionary(e => e.Pal.ModelObject, e => e.IsEnabled));
                    (window as Window).Close();
                },
                canExecute: (_) => HasChanges
            );

            CancelCommand = new RelayCommand<object>(
                execute: (window) =>
                {
                    DetachEvents();
                    onCancel?.Invoke();
                    (window as Window).Close();
                },
                canExecute: (_) => true
            );

            OpenPresetsMenuCommand = new RelayCommand(() => PresetsMenuIsOpen = true);
            if (presets != null)
                presets.PresetSelected += Presets_PresetSelected;
        }

        private void Presets_PresetSelected(IPalListPresetViewModel preset)
        {
            foreach (var e in allEntries)
            {
                e.IsEnabled = preset.Pals.Contains(e.Pal.ModelObject);
            }

            PresetsMenuIsOpen = false;
        }

        private void EntryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalCheckListEntryViewModel.HasChanges))
            {
                HasChanges = allEntries.Any(e => e.HasChanges);
            }

            if (e.PropertyName == nameof(PalCheckListEntryViewModel.IsEnabled))
            {
                OnPropertyChanged(nameof(AllItemsEnabled));

                if (Presets != null)
                    Presets.ActivePalSelections = allEntries.Where(e => e.IsEnabled).Select(e => e.Pal.ModelObject).ToList();
            }
        }

        private void DetachEvents()
        {
            foreach (var e in allEntries) e.PropertyChanged -= EntryPropertyChanged;
        }

        // for XAML designer preview
        [ObservableProperty]
        private ILocalizedText title = new HardCodedText("Pal Checklist");

        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [ObservableProperty]
        private bool hasChanges = false;

        public bool? AllItemsEnabled
        {
            get
            {
                if (allEntries.All(e => e.IsEnabled)) return true;
                if (allEntries.All(e => !e.IsEnabled)) return false;
                return null;
            }

            set
            {
                if (value == null) return;

                foreach (var e in allEntries)
                    e.IsEnabled = value.Value;
            }
        }

        [ObservableProperty]
        private bool presetsMenuIsOpen = false;

        public IRelayCommand OpenPresetsMenuCommand { get; }
    }
}
