﻿using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public SaveGameViewModel(ISaveGame value)
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
                Label = $"{value.GameId} (Unable to read metadata)";
            }

            IsValid = Value.IsValid;
        }

        public DateTime LastModified => Value.LastModified;

        public ISaveGame Value { get; }
        public CachedSaveGame CachedValue => Storage.LoadSave(Value, PalDB.LoadEmbedded());
        public string Label { get; }

        public bool IsValid { get; }

        public bool IsAddManualOption { get; }

        public Visibility WarningVisibility => !IsAddManualOption && !IsValid ? Visibility.Visible : Visibility.Collapsed;

        public static readonly SaveGameViewModel AddNewSave = new SaveGameViewModel();
    }
}
