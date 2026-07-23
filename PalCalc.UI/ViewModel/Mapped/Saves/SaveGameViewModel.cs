using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Mapped.Saves;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    public partial class SaveGameViewModel : ObservableObject
    {
        private static readonly ILogger logger = Log.ForContext<SaveGameViewModel>();

        private static SaveGameViewModel designerInstance;
        public static SaveGameViewModel DesignerInstance =>
            designerInstance ??= new SaveGameViewModel(ManualSaves.FromList([], null), CachedSaveGame.SampleForDesignerView.UnderlyingSave);

        public SaveGameViewModel(SavesCollectionViewModel parent, ISaveGame save)
        {
            Parent = parent;
            Type = parent.SaveType;
            Value = save;

            ReadSaveProperties();

            ReloadSaveCommand = new RelayCommand(() =>
            {
                Storage.ReloadSave(parent.SourceLocation, save, PalDB.LoadEmbedded(), GameSettingsViewModel.Load(save).ModelObject);
            });

            SubscribeToChanges(save);
        }

        private void SubscribeToChanges(ISaveGame save)
        {
            // TODO - Replace these self-cleaning weak callbacks with explicit save-session ownership.
            var weakSelf = new WeakReference<SaveGameViewModel>(this);

            Action<ISaveGame> saveUpdated = null;
            saveUpdated = _ =>
            {
                if (weakSelf.TryGetTarget(out var vm))
                    vm.QueueHasChanges();
                else
                    save.Updated -= saveUpdated;
            };
            save.Updated += saveUpdated;

            Action<ISaveGame> saveReloaded = null;
            saveReloaded = changedSave =>
            {
                if (weakSelf.TryGetTarget(out var vm))
                    vm.RespondToChanges(changedSave);
                else
                    Storage.SaveReloaded -= saveReloaded;
            };
            Storage.SaveReloaded += saveReloaded;

            Action<ISaveGame, CachedSaveGame> saveLoadEnded = null;
            saveLoadEnded = (changedSave, _) =>
            {
                if (weakSelf.TryGetTarget(out var vm))
                    vm.RespondToChanges(changedSave);
                else
                    CachedSaveGame.SaveFileLoadEnd -= saveLoadEnded;
            };
            CachedSaveGame.SaveFileLoadEnd += saveLoadEnded;
        }

        private void QueueHasChanges()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                HasChanges = true;
                return;
            }

            dispatcher.BeginInvoke(() => HasChanges = true);
        }

        private void RespondToChanges(ISaveGame changedSave)
        {
            if (changedSave != Value) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                UpdateAfterChange(changedSave);
                return;
            }

            dispatcher.BeginInvoke(() => UpdateAfterChange(changedSave));
        }

        private void UpdateAfterChange(ISaveGame changedSave)
        {
            ReadSaveProperties();
            HasChanges = !changedSave.IsLocal;
        }

        private void ReadSaveProperties()
        {
            try
            {
                var meta = Value.LevelMeta.ReadGameOptions();
                WorldName = meta.WorldName;
                DayNumber = meta.InGameDay;

                if (meta.IsServerSave)
                {
                    MainPlayerName = null;
                    MainPlayerLevel = null;
                    CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(new
                    {
                        DayNumber = meta.InGameDay,
                        WorldName = meta.WorldName,
                    });
                }
                else
                {
                    MainPlayerName = meta.PlayerName;
                    MainPlayerLevel = meta.PlayerLevel;
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
                WorldName = null;
                DayNumber = null;
                MainPlayerName = null;
                MainPlayerLevel = null;
                CombinedLabel = LocalizationCodes.LC_SAVE_GAME_LBL_NO_METADATA.Bind(Value.GameId);
            }

            IsValid = Value.IsValid;
            OnPropertyChanged(nameof(LastModified));
        }

        // TODO - Remove the need for this
        public SavesCollectionViewModel Parent { get; }

        public SaveType Type { get; set; }

        public ISaveGame Value { get; }
        public DateTime LastModified => Value.LastModified;

        [ObservableProperty]
        private bool isValid;

        [ObservableProperty]
        private ILocalizedText combinedLabel;

        public ObservableCollection<ILocalizedText> Warnings { get; set; }

        [ObservableProperty]
        private string worldName;

        [ObservableProperty]
        private string mainPlayerName;

        [ObservableProperty]
        private int? mainPlayerLevel;

        [ObservableProperty]
        private int? dayNumber;

        [ObservableProperty]
        private bool hasChanges;

        public SaveCustomizationsViewModel Customizations => SaveCustomizationsViewModel.GetOrCreate(Value);

        public CachedSaveGame CachedValue => Storage.LoadSave(Parent.SourceLocation, Value, PalDB.LoadEmbedded(), GameSettingsViewModel.Load(Value).ModelObject);

        public IRelayCommand OpenFolderCommand { get; set; }

        public IRelayCommand ReloadSaveCommand { get; }
    }
}
