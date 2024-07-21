using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.UI.Model;
using PalCalc.UI.View;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Media;

namespace PalCalc.UI.ViewModel
{
    public partial class PassiveSkillsPresetCollectionViewModel : ObservableObject
    {
        // for outer element to respond and close popup (if needed)
        public event Action<PassiveSkillsPresetViewModel> PresetSelected;

        // contains the currently selected passives from the outer element which would be used as new contents when saving/overwriting
        public PalTargetViewModel ActivePalTarget { get; set; }

        public IRelayCommand<IPassiveSkillsPresetViewModel> SelectPresetCommand { get; }
        public IRelayCommand<PassiveSkillsPresetViewModel> DeletePresetCommand { get; }
        public IRelayCommand<PassiveSkillsPresetViewModel> RenamePresetCommand { get; }
        public IRelayCommand<PassiveSkillsPresetViewModel> OverwritePresetCommand { get; }

        private ObservableCollection<IPassiveSkillsPresetViewModel> options;
        public ReadOnlyObservableCollection<IPassiveSkillsPresetViewModel> Options { get; }

        public static PassiveSkillsPresetCollectionViewModel DesignerInstance => new PassiveSkillsPresetCollectionViewModel([
            new PassiveSkillsPreset() { Name = "Test 1" },
            new PassiveSkillsPreset() { Name = "foo" },
        ]);

        public PassiveSkillsPresetCollectionViewModel(IEnumerable<PassiveSkillsPreset> initialOptions)
        {
            options = new ObservableCollection<IPassiveSkillsPresetViewModel>(
                initialOptions.Select(o => new PassiveSkillsPresetViewModel(o)).OrderBy(o => o.Label)
            );

            options.Insert(0, NewPassiveSkillsPresetViewModel.Instance);

            Options = new ReadOnlyObservableCollection<IPassiveSkillsPresetViewModel>(options);

            SelectPresetCommand = new RelayCommand<IPassiveSkillsPresetViewModel>(
                (presetVm) =>
                {
                    switch (presetVm)
                    {
                        case NewPassiveSkillsPresetViewModel:
                            var newNameWindow = new SimpleTextInputWindow()
                            {
                                Title = "New Preset Name",
                                InputLabel = "Name",
                                Result = "",
                                Validator = (name) => name.Length > 0 && !options.Any(o => o.Label == name),
                                Owner = App.Current.MainWindow,
                            };

                            if (newNameWindow.ShowDialog() == true)
                            {
                                var newName = newNameWindow.Result;

                                var newPreset = ActivePalTarget?.CurrentPalSpecifier?.ToPreset() ?? new PassiveSkillsPreset();
                                newPreset.Name = newName;

                                // insert alphabetically
                                var previous = Options.FirstOrDefault(o => o is PassiveSkillsPresetViewModel && o.Label.CompareTo(newName) > 0);
                                options.Insert(
                                    previous != null ? options.IndexOf(previous) : options.Count,
                                    new PassiveSkillsPresetViewModel(newPreset)
                                );

                                AppSettings.Current.PassiveSkillsPresets.Add(newPreset);
                                Storage.SaveAppSettings(AppSettings.Current);
                            }
                            break;

                        case PassiveSkillsPresetViewModel actualPreset:
                            PresetSelected?.Invoke(actualPreset);
                            break;
                    }
                }
            );

            DeletePresetCommand = new RelayCommand<PassiveSkillsPresetViewModel>(
                (presetVm) =>
                {
                    if (MessageBox.Show($"Delete the preset '{presetVm.Label}'?", "Delete Preset", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        options.Remove(presetVm);
                        AppSettings.Current.PassiveSkillsPresets.Remove(presetVm.ModelObject);
                        Storage.SaveAppSettings(AppSettings.Current);
                    }
                }
            );

            RenamePresetCommand = new RelayCommand<PassiveSkillsPresetViewModel>(
                (presetVm) =>
                {
                    var renameWindow = new SimpleTextInputWindow()
                    {
                        Title = $"Rename Preset '{presetVm.Label}'",
                        InputLabel = "Name",
                        Result = presetVm.Label,
                        Validator = (name) => name.Length > 0 && !options.Any(o => o.Label == name),
                        Owner = App.Current.MainWindow
                    };

                    if (renameWindow.ShowDialog() == true)
                    {
                        var newName = renameWindow.Result;

                        var preset = presetVm.ModelObject;
                        // note: reference to original preset from AppSettings object is preserved, just need to update the VM
                        preset.Name = newName;

                        options.Remove(presetVm);
                        // insert alphabetically
                        var previous = Options.FirstOrDefault(o => o is PassiveSkillsPresetViewModel && o.Label.CompareTo(newName) > 0);
                        options.Insert(
                            previous != null ? options.IndexOf(previous) : options.Count,
                            new PassiveSkillsPresetViewModel(preset)
                        );

                        Storage.SaveAppSettings(AppSettings.Current);
                    }
                }
            );

            OverwritePresetCommand = new RelayCommand<PassiveSkillsPresetViewModel>(
                (presetVm) =>
                {
                    if (MessageBox.Show($"Overwrite the preset '{presetVm.Label}' with the current selection of required and optional passive skills?", "Overwrite Preset", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
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
                }
            );
        }
    }
}
