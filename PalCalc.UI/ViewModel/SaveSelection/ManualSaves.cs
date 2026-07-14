using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Streaming.Adaptive;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal static class ManualSaves
    {
        public static SaveGameViewModel2 FromSave(StandardSaveGame save)
        {
            var res = SavesCommon.BuildNormalSave(
                save: save,
                openFolderCommand: new RelayCommand(() => WindowsUtils.OpenPathInExplorer(save.BasePath))
            );

            res.Type = SaveType.LocalFile;

            return res;
        }

        public static SavesCollectionViewModel FromList(IEnumerable<StandardSaveGame> existingSaves, ISavesService savesService)
        {
            var availableSaves = new ObservableCollection<SaveGameViewModel2>([
                .. existingSaves.Select(FromSave).OrderBy(sg => sg.CombinedLabel.Value)
            ]);

            return new SavesCollectionViewModel(
                SaveType: SaveType.LocalFile,
                AvailableSaves: new(availableSaves),
                TypeLabel: new HardCodedText("Local Files"), // TODO ITL
                Title: null,
                OpenFolderCommand: null,

                AddSaveCommand: new RelayCommand(() =>
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


                        if (availableSaves.Any(existing => existing.ModelObject.BasePath == asSaveGame.BasePath))
                        {
                            AdonisMessageBox.Show(App.Current.MainWindow, LocalizationCodes.LC_MANUAL_SAVE_ALREADY_REGISTERED.Bind().Value, caption: "");
                            return;
                        }

                        savesService.AddManualSave(asSaveGame);
                        var vm = FromSave(asSaveGame);
                        // insert while preserving alphanumeric ordering
                        var orderedIndex = availableSaves
                            .AsEnumerable()
                            .Append(vm)
                            .OrderBy(vm => vm.CombinedLabel.Value)
                            .ToList()
                            .IndexOf(vm);

                        availableSaves.Insert(orderedIndex, vm);
                    }
                }),

                RemoveSaveCommand: new RelayCommand<SaveGameViewModel2>((save) =>
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
                        savesService.RemoveManualSave(save.ModelObject as StandardSaveGame);
                    }
                })
            );
        }
    }
}
