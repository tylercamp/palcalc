using AdonisUI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.View.Utils;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;

namespace PalCalc.UI.ViewModel
{
    public partial class PassiveSkillsPresetCollectionViewModel : ObservableObject
    {
        // for outer element to respond and close popup (if needed)
        public event Action<PassiveSkillsPresetViewModel> PresetSelected;

        // contains the currently selected passives from the outer element which would be used as new contents when saving/overwriting
        public PalTargetViewModel ActivePalTarget { get; set; }

        public IRelayCommand<EditableListMenu.CreateCommandArgs> CreatePresetCommand { get; }
        public IRelayCommand<EditableListMenu.SelectCommandArgs> SelectPresetCommand { get; }
        public IRelayCommand<EditableListMenu.DeleteCommandArgs> DeletePresetCommand { get; }
        public IRelayCommand<EditableListMenu.RenameCommandArgs> RenamePresetCommand { get; }
        public IRelayCommand<EditableListMenu.OverwriteCommandArgs> OverwritePresetCommand { get; }

        private ObservableCollection<PassiveSkillsPresetViewModel> options;
        public ReadOnlyObservableCollection<PassiveSkillsPresetViewModel> Options { get; }

        public static PassiveSkillsPresetCollectionViewModel DesignerInstance => new PassiveSkillsPresetCollectionViewModel([
            new PassiveSkillsPreset() { Name = "Test 1" },
            new PassiveSkillsPreset() { Name = "foo" },
        ]);

        public PassiveSkillsPresetCollectionViewModel(IEnumerable<PassiveSkillsPreset> initialOptions)
        {
            options = new ObservableCollection<PassiveSkillsPresetViewModel>(
                initialOptions.Select(o => new PassiveSkillsPresetViewModel(o)).OrderBy(o => o.Label)
            );

            Options = new ReadOnlyObservableCollection<PassiveSkillsPresetViewModel>(options);

            CreatePresetCommand = new RelayCommand<EditableListMenu.CreateCommandArgs>(
                (ev) =>
                {
                    var newPreset = ActivePalTarget?.CurrentPalSpecifier?.ToPreset() ?? new PassiveSkillsPreset();
                    newPreset.Name = ev.NewName;

                    // insert alphabetically
                    var previous = Options.FirstOrDefault(o => o is PassiveSkillsPresetViewModel && o.Label.Value.CompareTo(ev.NewName) > 0);
                    options.Insert(
                        previous != null ? options.IndexOf(previous) : options.Count,
                        new PassiveSkillsPresetViewModel(newPreset)
                    );

                    AppSettings.Current.PassiveSkillsPresets.Add(newPreset);
                    Storage.SaveAppSettings(AppSettings.Current);
                }
            );

            SelectPresetCommand = new RelayCommand<EditableListMenu.SelectCommandArgs>(
                (ev) =>
                {
                    PresetSelected?.Invoke(ev.Item as PassiveSkillsPresetViewModel);
                }
            );

            DeletePresetCommand = new RelayCommand<EditableListMenu.DeleteCommandArgs>(
                (ev) =>
                {
                    var presetVm = ev.Item as PassiveSkillsPresetViewModel;

                    options.Remove(presetVm);
                    AppSettings.Current.PassiveSkillsPresets.Remove(presetVm.ModelObject);
                    Storage.SaveAppSettings(AppSettings.Current);
                }
            );

            RenamePresetCommand = new RelayCommand<EditableListMenu.RenameCommandArgs>(
                (ev) =>
                {
                    var presetVm = ev.Item as PassiveSkillsPresetViewModel;
                    var newName = ev.NewName;

                    var preset = presetVm.ModelObject;
                    // note: reference to original preset from AppSettings object is preserved, just need to update the VM
                    preset.Name = newName;

                    options.Remove(presetVm);
                    // insert alphabetically
                    var previous = Options.FirstOrDefault(o => o is PassiveSkillsPresetViewModel && o.Label.Value.CompareTo(newName) > 0);
                    options.Insert(
                        previous != null ? options.IndexOf(previous) : options.Count,
                        new PassiveSkillsPresetViewModel(preset)
                    );

                    Storage.SaveAppSettings(AppSettings.Current);
                }
            );

            OverwritePresetCommand = new RelayCommand<EditableListMenu.OverwriteCommandArgs>(
                (ev) =>
                {
                    var presetVm = ev.Item as PassiveSkillsPresetViewModel;

                    var oldPreset = presetVm.ModelObject;
                    var newPreset = ActivePalTarget?.CurrentPalSpecifier?.ToPreset() ?? new PassiveSkillsPreset();
                    newPreset.Name = oldPreset.Name;

                    AppSettings.Current.PassiveSkillsPresets.Remove(oldPreset);
                    AppSettings.Current.PassiveSkillsPresets.Add(newPreset);

                    var idx = options.IndexOf(presetVm);
                    options.Remove(presetVm);
                    options.Insert(idx, new PassiveSkillsPresetViewModel(newPreset));

                    Storage.SaveAppSettings(AppSettings.Current);
                }
            );
        }
    }
}
