using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.View.Utils;
using PalCalc.UI.ViewModel.Presets.BuiltIn.PalList;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Presets
{
    public interface IPalListPresetViewModel
    {
        public List<Pal> Pals { get; }
    }

    public partial class PalListPresetCollectionViewModel : ObservableObject
    {
        public event Action<IPalListPresetViewModel> PresetSelected;

        public List<Pal> ActivePalSelections { get; set; }

        public IRelayCommand<EditableListMenu.CreateCommandArgs> CreateCommand { get; set; }
        public IRelayCommand<EditableListMenu.DeleteCommandArgs> DeleteCommand { get; set; }
        public IRelayCommand<EditableListMenu.OverwriteCommandArgs> OverwriteCommand { get; set; }
        public IRelayCommand<EditableListMenu.RenameCommandArgs> RenameCommand { get; set; }
        public IRelayCommand<EditableListMenu.SelectCommandArgs> SelectCommand { get; set; }

        public ObservableCollection<CustomPalListPresetViewModel> Options { get; }
        public ObservableCollection<BuiltInPalListPresetViewModel> BuiltInOptions { get; }

        private void InsertOption(CustomPalListPresetViewModel option)
        {
            // insert alphabetically
            var previous = Options.FirstOrDefault(o => o.Name.CompareTo(option.Name) > 0);
            Options.Insert(
                previous != null ? Options.IndexOf(previous) : Options.Count,
                option
            );
        }

        public PalListPresetCollectionViewModel(CachedSaveGame context, IPalSource availablePalFilter, IEnumerable<PalListPreset> initialOptions)
        {
            Options = new ObservableCollection<CustomPalListPresetViewModel>(
                (initialOptions ?? []).Select(p => new CustomPalListPresetViewModel(p)).OrderBy(o => o.Name)
            );

            BuiltInOptions = new ObservableCollection<BuiltInPalListPresetViewModel>(BuiltInPalListPresetViewModel.BuildAll(context, availablePalFilter));

            CreateCommand = new RelayCommand<EditableListMenu.CreateCommandArgs>(
                (ev) =>
                {
                    var newPreset = new CustomPalListPresetViewModel()
                    {
                        Name = ev.NewName,
                        Pals = ActivePalSelections
                    };

                    InsertOption(newPreset);

                    AppSettings.Current.PalListPresets.Add(newPreset.AsModelObject);
                    Storage.SaveAppSettings(AppSettings.Current);
                }
            );

            DeleteCommand = new RelayCommand<EditableListMenu.DeleteCommandArgs>(
                (ev) =>
                {
                    var vm = ev.Item as CustomPalListPresetViewModel;
                    Options.Remove(vm);
                    AppSettings.Current.PalListPresets.RemoveAll(p => p.Name == vm.Name);
                    Storage.SaveAppSettings(AppSettings.Current);
                }
            );

            OverwriteCommand = new RelayCommand<EditableListMenu.OverwriteCommandArgs>(
                (ev) =>
                {
                    var vm = ev.Item as CustomPalListPresetViewModel;

                    vm.Pals = ActivePalSelections;

                    AppSettings.Current.PalListPresets.RemoveAll(p => p.Name == vm.Name);
                    AppSettings.Current.PalListPresets.Add(vm.AsModelObject);

                    Storage.SaveAppSettings(AppSettings.Current);
                }
            );

            RenameCommand = new RelayCommand<EditableListMenu.RenameCommandArgs>(
                (ev) =>
                {
                    var vm = ev.Item as CustomPalListPresetViewModel;
                    Options.Remove(vm);

                    AppSettings.Current.PalListPresets.Find(p => p.Name == vm.Name).Name = ev.NewName;
                    Storage.SaveAppSettings(AppSettings.Current);

                    vm.Name = ev.NewName;
                    InsertOption(vm);
                }
            );

            SelectCommand = new RelayCommand<EditableListMenu.SelectCommandArgs>(
                (ev) => PresetSelected?.Invoke(ev.Item as IPalListPresetViewModel)
            );
        }
    }
}
