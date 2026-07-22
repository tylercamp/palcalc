using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.CSV;
using PalCalc.UI.View;
using PalCalc.UI.View.Inspector;
using PalCalc.UI.ViewModel.Inspector;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.SaveSelection;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;

namespace PalCalc.UI.ViewModel
{
    internal partial class CommonSaveOperationsViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<CommonSaveOperationsViewModel>();

        private readonly IRelayCommand navigateSaveSelectionPageCommand;
        private readonly SavesCollectionViewModel selectedLocation;
        private readonly SaveGameViewModel2 selectedSave;

        public CommonSaveOperationsViewModel(
            IRelayCommand navigateSaveSelectionPageCommand,
            SavesCollectionViewModel selectedLocation,
            SaveGameViewModel2 selectedSave
        )
        {
            this.navigateSaveSelectionPageCommand = navigateSaveSelectionPageCommand;
            this.selectedLocation = selectedLocation;
            this.selectedSave = selectedSave;

            if (selectedSave != null)
            {
                PropertyChangedEventManager.AddHandler(
                    selectedSave,
                    SelectedSave_PropertyChanged,
                    nameof(SaveGameViewModel2.IsValid)
                );
            }
        }

        public static CommonSaveOperationsViewModel DesignerInstance { get; } = new CommonSaveOperationsViewModel(null, null, null);

        [ObservableProperty]
        private bool menuIsOpen = false;

        public IRelayCommand NavigateSaveSelectionPageCommand => navigateSaveSelectionPageCommand;

        private void SelectedSave_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ExportSaveCsvCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void OpenMenu()
        {
            MenuIsOpen = true;
        }

        [RelayCommand(CanExecute = nameof(CanExportSave))]
        private void ExportSave()
        {
            var sfd = new SaveFileDialog()
            {
                FileName = $"Palworld-{CachedSaveGame.IdentifierFor(selectedSave.Value)}.zip",
                Filter = "ZIP | *.zip",
                AddExtension = true,
                DefaultExt = "zip"
            };

            if (sfd.ShowDialog() == true)
            {
                using (var outStream = new FileStream(sfd.FileName, FileMode.Create))
                using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create))
                {
                    var save = selectedSave.Value;

                    void Export(ISaveFile file, string basePath)
                    {
                        var filePaths = file.FilePaths.ToArray();
                        if (filePaths.Length == 1)
                        {
                            archive.CreateEntryFromFile(filePaths[0], $"{basePath}.sav");
                        }
                        else
                        {
                            for (int i = 0; i < filePaths.Length; i++)
                            {
                                archive.CreateEntryFromFile(filePaths[i], $"{basePath}-{i}.sav");
                            }
                        }
                    }

                    void ExportRaw(SaveFileLocation rawFile)
                    {
                        archive.CreateEntryFromFile(rawFile.ActualPath, $"_raw_/{rawFile.NormalizedPath}");
                    }

                    if (save.Level != null && save.Level.Exists)
                        Export(save.Level, "Level");

                    if (save.LevelMeta != null && save.LevelMeta.Exists)
                        Export(save.LevelMeta, "LevelMeta");

                    if (save.WorldOption != null && save.WorldOption.Exists)
                        Export(save.WorldOption, "WorldOption");

                    if (save.LocalData != null && save.LocalData.Exists)
                        Export(save.LocalData, "LocalData");

                    foreach (var player in save.Players.Where(p => p.Exists))
                    {
                        string playerId;
                        try
                        {
                            playerId = player.ReadPlayerContent().PlayerId;
                        }
                        catch
                        {
                            playerId = Path.GetFileNameWithoutExtension(player.FilePaths.First());
                        }
                        Export(player, $"Players/{playerId}");
                    }

                    foreach (var rawFile in save.RawFiles)
                        ExportRaw(rawFile);
                }
            }
        }
        private bool CanExportSave() => selectedSave != null;

        [RelayCommand(CanExecute = nameof(CanExportSaveCsv))]
        private void ExportSaveCsv()
        {
            var sfd = new SaveFileDialog()
            {
                FileName = $"Pals.csv",
                Filter = "CSV | *.csv",
                AddExtension = true,
                DefaultExt = "csv"
            };

            if (sfd.ShowDialog() == true)
            {
                var cachedSave = selectedSave.CachedValue;
                if (cachedSave == null)
                {
                    // TODO - ITL
                    AdonisMessageBox.Show("The save could not be loaded.", caption: "");
                    return;
                }

                File.WriteAllText(sfd.FileName, PalCSVExporter.Export(cachedSave, GameSettingsViewModel.Load(selectedSave.Value).ModelObject));
            }
        }
        private bool CanExportSaveCsv() => selectedSave?.Value?.IsValid == true;

        [RelayCommand(CanExecute = nameof(CanInspectSave))]
        private void InspectSave()
        {
            var loadingModal = new LoadingSaveFileModal();
            loadingModal.Owner = App.Current.MainWindow;
            loadingModal.DataContext = LocalizationCodes.LC_SAVE_INSPECTOR_LOADING.Bind();

            try
            {
                var vm = loadingModal.ShowDialogDuring(
                    () => new SaveInspectorWindowViewModel(selectedLocation, selectedSave, GameSettingsViewModel.Load(selectedSave.Value).ModelObject)
                );

                var inspector = new SaveInspectorWindow() { DataContext = vm, Owner = App.Current.MainWindow };
                SaveInspectorWindowManager.Register(selectedSave.Value, inspector);
                inspector.Show();
            }
            catch (Exception e)
            {
                logger.Error(e, "Error loading save inspector data");
                // TODO - ITL
                AdonisMessageBox.Show("The save inspector could not be opened.", caption: "");
            }
        }
        private bool CanInspectSave() => selectedSave != null;
    }
}
