using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI.ViewModel.Mapped
{
    public partial class SaveGameViewModel
    {
        private static ILogger logger = Log.ForContext<SaveGameViewModel>();

        private SaveGameViewModel()
        {
            IsAddManualOption = true;
            Label = "Add a new save...";
        }

        private SaveGameViewModel(SaveGame value)
        {
            IsAddManualOption = false;
            Value = value;

            try
            {
                var meta = value.LevelMeta.ReadGameOptions();
                Label = meta.ToString();
                //Label = $"{meta.PlayerName} lv. {meta.PlayerLevel} in {meta.WorldName}";
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "error when loading LevelMeta for {saveId}", CachedSaveGame.IdentifierFor(value));
                Label = $"{value.FolderName} (Unable to read metadata)";
            }
        }

        private static Dictionary<string, SaveGameViewModel> vms = new Dictionary<string, SaveGameViewModel>();
        public static SaveGameViewModel FromSave(SaveGame value)
        {
            if (vms.ContainsKey(value.BasePath)) return vms[value.BasePath];

            var vm = new SaveGameViewModel(value);
            vms.Add(value.BasePath, vm);
            return vm;
        }

        public DateTime LastModified => Value.LastModified;

        public SaveGame Value { get; }
        public CachedSaveGame CachedValue => Storage.LoadSave(Value, PalDB.LoadEmbedded());
        public string Label { get; }

        public bool IsValid => Value.IsValid;

        public bool IsAddManualOption { get; }

        public Visibility WarningVisibility => !IsAddManualOption && !IsValid ? Visibility.Visible : Visibility.Collapsed;

        public static readonly SaveGameViewModel AddNewSave = new SaveGameViewModel();
    }
}
