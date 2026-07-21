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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using AdonisMessageBox = AdonisUI.Controls.MessageBox;

namespace PalCalc.UI.ViewModel
{
    internal partial class ToolbarViewModel(Dispatcher dispatcher, AppUpdatesViewModel appUpdates, IRelayCommand navigateSaveSelectionPageCommand) : ObservableObject
    {
        private static ToolbarViewModel designerInstance;
        public static ToolbarViewModel DesignerInstance => designerInstance ??= new(Dispatcher.CurrentDispatcher, null, new RelayCommand(() => { }));

        private static ILogger logger = Log.ForContext<ToolbarViewModel>();

        public IRelayCommand NavigateSaveSelectionPageCommand => navigateSaveSelectionPageCommand;

        public List<TranslationLocaleViewModel> Locales { get; } =
            Enum
                .GetValues<TranslationLocale>()
                .Select(l => new TranslationLocaleViewModel(l))
                .ToList();

        [NotifyCanExecuteChangedFor(nameof(ExportSaveCommand))]
        [NotifyCanExecuteChangedFor(nameof(ExportSaveCsvCommand))]
        [NotifyCanExecuteChangedFor(nameof(InspectSaveCommand))]
        [ObservableProperty]
        private SaveGameViewModel2 selectedSave;

        [ObservableProperty]
        private SavesCollectionViewModel selectedLocation;

        [RelayCommand(CanExecute = nameof(CanExportSave))]
        private void ExportSave()
        {
            var sfd = new SaveFileDialog()
            {
                FileName = $"Palworld-{CachedSaveGame.IdentifierFor(SelectedSave.Value)}.zip",
                Filter = "ZIP | *.zip",
                AddExtension = true,
                DefaultExt = "zip"
            };

            if (sfd.ShowDialog() == true)
            {
                using (var outStream = new FileStream(sfd.FileName, FileMode.Create))
                using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create))
                {
                    var save = SelectedSave.Value;

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
        private bool CanExportSave() => SelectedSave != null;

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
                // TODO - ensure CachedValue is valid
                File.WriteAllText(sfd.FileName, PalCSVExporter.Export(SelectedSave.CachedValue, GameSettingsViewModel.Load(SelectedSave.Value).ModelObject));
            }
        }
        private bool CanExportSaveCsv() => SelectedSave != null;

        [RelayCommand(CanExecute = nameof(CanInspectSave))]
        private void InspectSave()
        {
            var loadingModal = new LoadingSaveFileModal();
            loadingModal.Owner = App.Current.MainWindow;
            loadingModal.DataContext = LocalizationCodes.LC_SAVE_INSPECTOR_LOADING.Bind();

            // TODO - ensure CachedValue is valid

            var vm = loadingModal.ShowDialogDuring(
                () => new SaveInspectorWindowViewModel(SelectedLocation, SelectedSave, GameSettingsViewModel.Load(SelectedSave.Value).ModelObject)
            );

            var inspector = new SaveInspectorWindow() { DataContext = vm, Owner = App.Current.MainWindow };
            inspector.Show();
        }
        private bool CanInspectSave() => SelectedSave != null;

        [RelayCommand]
        private void ExportCrashLog()
        {
            var sfd = new SaveFileDialog();
            sfd.FileName = "CRASHLOG.zip";
            sfd.Filter = "ZIP | *.zip";
            sfd.AddExtension = true;
            sfd.DefaultExt = "zip";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    CrashSupport.PrepareSupportFile(sfd.FileName);
                }
                catch (Exception e)
                {
                    logger.Warning(e, "unexpected error when attempting to create crashlog file");
                    AdonisMessageBox.Show(LocalizationCodes.LC_CRASHLOG_FAILED.Bind().Value, caption: "");
                }
            }
        }

        [RelayCommand]
        private void OpenAboutWindow()
        {
            var window = new AboutWindow();
            window.Owner = App.Current.MainWindow;
            window.ShowDialog();
        }

        [RelayCommand]
        private void ForceCheckForUpdates()
        {
            Task.Run(async () =>
            {
                try
                {
                    var newVersion = await appUpdates.FetchNewUpdateUrl();

                    dispatcher.BeginInvoke(() =>
                    {
                        if (newVersion == null)
                        {
                            AdonisMessageBox.Show("Pal Calc is up to date!");
                            return;
                        }

                        appUpdates.PromptUpdateDownload(newVersion);
                    }, DispatcherPriority.ContextIdle);
                }
                catch (Exception e)
                {
                    // TODO
                }
            });
        }
    }
}
