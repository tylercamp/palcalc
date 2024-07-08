using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Mapped;
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
        public string PalName => Pal.Name;

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
                null, null,
                new Dictionary<Pal, bool>()
                {
                    { PalDB.LoadEmbedded().Pals.First(), true }
                }
            );

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
                        VisibleEntries = allEntries.Where(e => e.PalName.ToLower().Contains(value.ToLower())).ToList();
                    }
                    else
                    {
                        VisibleEntries = allEntries;
                    }
                }
            }
        }
        
        public PalCheckListViewModel(Action onCancel, Action<Dictionary<Pal, bool>> onSave, Dictionary<Pal, bool> initialState)
        {
            // TODO - handle user clicking "X" button on window
            allEntries = initialState.Select(kvp => new PalCheckListEntryViewModel(new PalViewModel(kvp.Key), kvp.Value)).ToList();
            VisibleEntries = allEntries;

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
        }

        private void EntryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalCheckListEntryViewModel.HasChanges))
            {
                HasChanges = allEntries.Any(e => e.HasChanges);
            }
        }

        private void DetachEvents()
        {
            foreach (var e in allEntries) e.PropertyChanged -= EntryPropertyChanged;
        }

        [ObservableProperty]
        private string title = "Pal Checklist";

        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [ObservableProperty]
        private bool hasChanges = false;
    }
}
