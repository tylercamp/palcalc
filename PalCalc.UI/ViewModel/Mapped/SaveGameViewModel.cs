using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel.Mapped
{
    public interface ISaveGameViewModel : INotifyPropertyChanged
    {
        public ILocalizedText Label { get; }
        public bool HasWarnings { get; }
    }

    public partial class NewManualSaveGameViewModel : ObservableObject, ISaveGameViewModel
    {
        public NewManualSaveGameViewModel()
        {
            Label = LocalizationCodes.LC_ADD_NEW_SAVE.Bind();
        }

        public ILocalizedText Label { get; }
        public bool HasWarnings => false;
    }

    public partial class NewFakeSaveGameViewModel : ObservableObject, ISaveGameViewModel
    {
        public NewFakeSaveGameViewModel()
        {
            Label = LocalizationCodes.LC_ADD_FAKE_SAVE.Bind();
        }

        public ILocalizedText Label { get; }
        public bool HasWarnings => false;
    }

    public partial class SaveGameViewModel : ObservableObject, ISaveGameViewModel
    {
        private static ILogger logger = Log.ForContext<SaveGameViewModel>();

        private static SaveGameViewModel designerInstance;
        public static SaveGameViewModel DesignerInstance =>
            designerInstance ??= new SaveGameViewModel(CachedSaveGame.SampleForDesignerView.UnderlyingSave);

        public SaveGameViewModel(ISaveGame value)
        {
            Value = value;

            ReadSaveDescription();

            value.Updated += (_) =>
            {
                if (!HasChanges)
                    App.Current.Dispatcher.BeginInvoke(() => HasChanges = true);
            };

            ReloadSaveCommand = new RelayCommand(() =>
            {
                Storage.ReloadSave(value, PalDB.LoadEmbedded());
            });

            Storage.SaveReloaded += RespondToChanges;
            CachedSaveGame.SaveFileLoadEnd += (save, cached) => RespondToChanges(save);
        }

        private void RespondToChanges(ISaveGame changedSave)
        {
            if (changedSave == value)
            {
                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    ReadSaveDescription();
                    if (changedSave.IsLocal)
                        HasChanges = false;
                    else 
                        HasChanges = true;
                });
            }
        }

        private void ReadSaveDescription()
        {
            try
            {
                var meta = Value.LevelMeta.ReadGameOptions();
                if (meta.IsServerSave)
                {
                    Label = LocalizationCodes.LC_SAVE_GAME_LBL_SERVER.Bind(new
                    {
                        DayNumber = meta.InGameDay,
                        WorldName = meta.WorldName,
                    });
                }
                else
                {
                    Label = LocalizationCodes.LC_SAVE_GAME_LBL.Bind(new
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
                Label = LocalizationCodes.LC_SAVE_GAME_LBL_NO_METADATA.Bind(Value.GameId);
            }

            IsValid = Value.IsValid;
        }

        public DateTime LastModified => Value.LastModified;

        private ISaveGame value;
        public ISaveGame Value
        {
            get => value;
            private set => SetProperty(ref this.value, value);
        }

        public CachedSaveGame CachedValue => Storage.LoadSave(Value, PalDB.LoadEmbedded());

        private SaveCustomizationsViewModel customizations = null;
        public SaveCustomizationsViewModel Customizations => customizations ??= new SaveCustomizationsViewModel(this);

        private ILocalizedText label;
        public ILocalizedText Label
        {
            get => label;
            private set => SetProperty(ref label, value);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasWarnings))]
        private bool isValid;

        public bool HasWarnings => !IsValid;

        private bool hasChanges;
        public bool HasChanges
        {
            get => hasChanges;
            private set => SetProperty(ref hasChanges, value);
        }

        public IRelayCommand ReloadSaveCommand { get; }
    }
}
