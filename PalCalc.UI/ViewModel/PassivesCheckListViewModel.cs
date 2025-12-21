using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    public partial class PassivesCheckListEntryViewModel : ObservableObject
    {
        private bool initialEnabled;
        public PassivesCheckListEntryViewModel(PassiveSkillViewModel passive, bool initialEnabled)
        {
            Passive = passive;
            this.initialEnabled = initialEnabled;

            isEnabled = initialEnabled;

            ItemRequiredLabel = passive.ModelObject.SurgeryRequiredItem == null
                ? LocalizationCodes.LC_PASSIVES_CHECKLIST_SURGERY_ITEM_NO.Bind()
                : LocalizationCodes.LC_PASSIVES_CHECKLIST_SURGERY_ITEM_YES.Bind();
        }

        public PassiveSkillViewModel Passive { get; }

        [NotifyPropertyChangedFor(nameof(HasChanges))]
        [ObservableProperty]
        private bool isEnabled;

        public bool HasChanges => IsEnabled != initialEnabled;

        public ILocalizedText ItemRequiredLabel { get; }
    }

    public partial class PassivesCheckListViewModel : ObservableObject
    {
        public IRelayCommand<object> SaveCommand { get; }
        public IRelayCommand<object> CancelCommand { get; }

        public static PassivesCheckListViewModel DesignerInstance { get; } =
            new PassivesCheckListViewModel(
                null, null,
                PalDB.LoadEmbedded().PassiveSkills.Where(p => p.SupportsSurgery).ToDictionary(p => p, p => true)
            );

        private List<PassivesCheckListEntryViewModel> allEntries;
        public IReadOnlyCollection<PassivesCheckListEntryViewModel> AllEntries => allEntries;

        public PassivesCheckListViewModel(Action onCancel, Action<Dictionary<PassiveSkill, bool>> onSave, Dictionary<PassiveSkill, bool> initialState)
        {
            allEntries = initialState
                .Select(kvp => new PassivesCheckListEntryViewModel(PassiveSkillViewModel.Make(kvp.Key), kvp.Value))
                .OrderBy(vm => vm.Passive.Name.Value)
                .ToList();

            foreach (var e in allEntries)
                e.PropertyChanged += EntryPropertyChanged;

            SaveCommand = new RelayCommand<object>(
                execute: (window) =>
                {
                    DetachEvents();
                    onSave?.Invoke(allEntries.ToDictionary(e => e.Passive.ModelObject, e => e.IsEnabled));
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
        }

        private void EntryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PassivesCheckListEntryViewModel.HasChanges))
            {
                HasChanges = allEntries.Any(e => e.HasChanges);
            }

            if (e.PropertyName == nameof(PassivesCheckListEntryViewModel.IsEnabled))
            {
                OnPropertyChanged(nameof(AllItemsEnabled));
            }
        }

        private void DetachEvents()
        {
            foreach (var e in allEntries) e.PropertyChanged -= EntryPropertyChanged;
        }

        // for XAML designer preview
        [ObservableProperty]
        private ILocalizedText title = new HardCodedText("Passives Checklist");

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
    }
}
