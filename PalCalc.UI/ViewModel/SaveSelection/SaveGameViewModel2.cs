using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    public class SaveGameViewModel2
    {
        private static ILogger logger = Log.ForContext<SaveGameViewModel2>();

        private SavesCollectionViewModel parentLocation;

        private static SaveGameViewModel2 designerInstance;
        public static SaveGameViewModel2 DesignerInstance =>
            designerInstance ??= new SaveGameViewModel2(ManualSaves.FromList([], null), CachedSaveGame.SampleForDesignerView.UnderlyingSave);

        public SaveGameViewModel2(SavesCollectionViewModel parent, ISaveGame save)
        {
            parentLocation = parent;

            Type = parent.SaveType;
            Value = save;
            IsValid = save.IsValid;

            ReadSaveProperties();

            save.Updated += (_) =>
            {
                if (!HasChanges)
                    App.Current.Dispatcher.BeginInvoke(() => HasChanges = true);
            };

            ReloadSaveCommand = new RelayCommand(() =>
            {
                Storage.ReloadSave(parent.SourceLocation, save, PalDB.LoadEmbedded(), GameSettingsViewModel.Load(save).ModelObject);
            });

            Customizations = new SaveCustomizationsViewModel(this);

            Storage.SaveReloaded += RespondToChanges;
            CachedSaveGame.SaveFileLoadEnd += (save, cached) => RespondToChanges(save);
        }

        private void RespondToChanges(ISaveGame changedSave)
        {
            if (changedSave == Value)
            {
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    ReadSaveProperties();
                    if (changedSave.IsLocal)
                        HasChanges = false;
                    else
                        HasChanges = true;
                });
            }
        }

        private void ReadSaveProperties()
        {
            try
            {
                var meta = Value.LevelMeta.ReadGameOptions();
                if (meta.IsServerSave)
                {
                    CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(new
                    {
                        DayNumber = meta.InGameDay,
                        WorldName = meta.WorldName,
                    });
                }
                else
                {
                    CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL.Bind(new
                    {
                        PlayerName = meta.PlayerName,
                        PlayerLevel = meta.PlayerLevel,
                        DayNumber = meta.InGameDay,
                        WorldName = meta.WorldName,
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "error when loading LevelMeta for {saveId}", CachedSaveGame.IdentifierFor(Value));
                CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_NO_METADATA.Bind(Value.GameId);
            }

            IsValid = Value.IsValid;
        }

        public SaveType Type { get; set; }

        public ISaveGame Value { get; set; }
        public bool IsValid { get; set; }

        public ILocalizedText CombinedLabel { get; set; }

        public ObservableCollection<ILocalizedText> Warnings { get; set; }

        public string WorldName { get; set; }
        public string MainPlayerName { get; set; }
        public int? MainPlayerLevel { get; set; }
        public int? DayNumber { get; set; }

        public bool HasChanges { get; private set; }

        public SaveCustomizationsViewModel Customizations { get; }

        public CachedSaveGame CachedValue => Storage.LoadSave(parentLocation.SourceLocation, Value, PalDB.LoadEmbedded(), GameSettingsViewModel.Load(Value).ModelObject);

        public IRelayCommand OpenFolderCommand { get; set; }

        public IRelayCommand ReloadSaveCommand { get; }
    }
}
