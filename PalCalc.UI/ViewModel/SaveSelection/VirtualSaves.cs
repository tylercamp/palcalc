using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View.Utils;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal static class VirtualSaves
    {
        public static SavesCollectionViewModel CollectAll(AppSettings sourceSettings, ISavesService savesService)
        {
            return FromList(
                sourceSettings.FakeSaveNames.Select(FakeSaveGame.Create),
                savesService
            );
        }

        public static SaveGameViewModel2 FromSave(SavesCollectionViewModel parent, VirtualSaveGame save)
        {
            var meta = save.LevelMeta.ReadGameOptions();

            return new SaveGameViewModel2(parent, save)
            {
                Value = save,
                IsValid = true,
                CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(
                    // (InGameDay is always default 0, but the format param is required for this translation code)
                    new { WorldName = meta.WorldName, DayNumber = meta.InGameDay }
                ),
                Warnings = [],
                WorldName = meta.WorldName,
                DayNumber = meta.InGameDay,
                MainPlayerName = null,
                MainPlayerLevel = null,
                OpenFolderCommand = null
            };
        }

        public static SavesCollectionViewModel FromList(IEnumerable<VirtualSaveGame> saves, ISavesService savesService)
        {
            var res = new SavesCollectionViewModel();
            res.SourceLocation = null;
            res.SaveType = SaveType.Virtual;
            res.TypeLabel = new HardCodedText("Fake Saves"); // TODO ITL
            res.Title = null;
            res.OpenFolderCommand = null;

            var availableSaves = new ObservableCollection<SaveGameViewModel2>(
                [.. saves.Select(sg => FromSave(res, sg)).OrderBy(s => s.CombinedLabel.Value)]
            );
            res.AvailableSaves = new(availableSaves);

            res.AddSaveCommand = new RelayCommand(() =>
            {
                var existingSaveNames =
                    availableSaves
                        .Select(s => s.Value)
                        .OfType<VirtualSaveGame>()
                        .Select(FakeSaveGame.GetLabel)
                        .ToList();

                var window = new SimpleTextInputWindow()
                {
                    Title = LocalizationCodes.LC_CUSTOM_SAVE_GAME_NAME.Bind().Value,
                    InputLabel = LocalizationCodes.LC_CUSTOM_SAVE_GAME_NAME_LABEL.Bind().Value,
                    Validator = name => name.Length > 0 && !existingSaveNames.Contains(name),
                    Owner = App.ActiveWindow,
                };

                if (window.ShowDialog() != true)
                {
                    return;
                }

                var saveGame = FakeSaveGame.Create(window.Result) as VirtualSaveGame;
                savesService.AddVirtualSave(saveGame);

                var vm = FromSave(res, saveGame);
                // insert while preserving alphanumeric ordering
                var orderedIndex = availableSaves
                    .AsEnumerable()
                    .Append(vm)
                    .OrderBy(vm => vm.CombinedLabel.Value)
                    .ToList()
                    .IndexOf(vm);

                availableSaves.Insert(orderedIndex, vm);
            });

            res.RemoveSaveCommand = new RelayCommand<SaveGameViewModel2>((save) =>
            {
                var confirmation = AdonisMessageBox.Show(
                    App.ActiveWindow,
                    LocalizationCodes.LC_REMOVE_SAVE_DESCRIPTION.Bind(save.CombinedLabel).Value,
                    LocalizationCodes.LC_REMOVE_SAVE_TITLE.Bind().Value,
                    AdonisMessageBoxButton.YesNo
                );

                // TODO - when going from solver page back to save selector page, need to
                //        close all open Inspector (and Passives List) windows
                if (confirmation == AdonisMessageBoxResult.Yes)
                {
                    availableSaves.Remove(save);
                    savesService.RemoveVirtualSave(save.Value as VirtualSaveGame);
                }
            });

            return res;
        }
    }
}
