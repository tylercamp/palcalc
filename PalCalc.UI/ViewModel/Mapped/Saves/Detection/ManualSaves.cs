using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.SaveSelection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI.ViewModel.Mapped.Saves.Detection
{
    internal static class ManualSaves
    {
        public static SavesCollectionViewModel CollectAll(AppSettings sourceSettings, ISavesService savesService)
        {
            return FromList(
                sourceSettings.ExtraSaveLocations.Select(saveFolder => new StandardSaveGame(saveFolder)),
                savesService
            );
        }

        public static SaveGameViewModel FromSave(SavesCollectionViewModel parent, StandardSaveGame save)
        {
            var res = SavesCommon.BuildNormalSave(
                parent: parent,
                save: save,
                openFolderCommand: new RelayCommand(() => WindowsUtils.OpenPathInExplorer(save.BasePath))
            );

            res.Type = SaveType.LocalFile;

            return res;
        }

        public static SavesCollectionViewModel FromList(IEnumerable<StandardSaveGame> existingSaves, ISavesService savesService)
        {
            var res = new SavesCollectionViewModel()
            {
                SaveType = SaveType.LocalFile,
                TypeLabel = LocalizationCodes.LC_SAVE_KIND_TITLE_LOCAL_FILES.Bind(),
                Title = null,
                OpenFolderCommand = null,
            };

            var availableSaves = new ObservableCollection<SaveGameViewModel>([
                .. existingSaves.Select(sg => FromSave(res, sg)).OrderBy(sg => sg.CombinedLabel.Value)
            ]);
            res.AvailableSaves = new(availableSaves);

            res.AddSaveCommand = new RelayCommand(() =>
            {
                var ofd = new OpenFileDialog();
                ofd.Filter = LocalizationCodes.LC_MANUAL_SAVE_EXTENSION_LBL.Bind().Value + "|Level*.sav";
                ofd.Title = LocalizationCodes.LC_MANUAL_SAVE_SELECTOR_TITLE.Bind().Value;

                if (true == ofd.ShowDialog(App.Current.MainWindow))
                {
                    var asSaveGame = new StandardSaveGame(Path.GetDirectoryName(ofd.FileName));
                    if (!asSaveGame.IsValid)
                    {
                        AdonisMessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_MANUAL_SAVE_INCOMPLETE.Bind().Value, caption: "");
                        return;
                    }


                    if (availableSaves.Any(existing => existing.Value.BasePath == asSaveGame.BasePath))
                    {
                        AdonisMessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_MANUAL_SAVE_ALREADY_REGISTERED.Bind().Value, caption: "");
                        return;
                    }

                    savesService.AddManualSave(asSaveGame);
                    var vm = FromSave(res, asSaveGame);
                    // insert while preserving alphanumeric ordering
                    var orderedIndex = availableSaves
                        .AsEnumerable()
                        .Append(vm)
                        .OrderBy(vm => vm.CombinedLabel.Value)
                        .ToList()
                        .IndexOf(vm);

                    availableSaves.Insert(orderedIndex, vm);
                }
            });

            res.RemoveSaveCommand = new RelayCommand<SaveGameViewModel>((save) =>
            {
                var confirmation = AdonisMessageBox.Show(
                    App.ActiveWindow,
                    LocalizationCodes.LC_REMOVE_SAVE_DESCRIPTION.Bind(save.CombinedLabel).Value,
                    LocalizationCodes.LC_REMOVE_SAVE_TITLE.Bind().Value,
                    AdonisMessageBoxButton.YesNo
                );

                if (confirmation == AdonisMessageBoxResult.Yes)
                {
                    availableSaves.Remove(save);
                    savesService.RemoveManualSave(save.Value as StandardSaveGame);
                }
            });

            return res;
        }
    }
}
