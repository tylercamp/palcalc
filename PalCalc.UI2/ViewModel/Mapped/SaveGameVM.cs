using Avalonia.Logging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI2.Model;
using PalCalc.UI2.Model.Storage;
using PalCalc.UI2.ViewModel;
using PalCalc.UI2.ViewModel.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.ViewModels.Mapped
{
    internal class SaveGameInfoVM : BaseVM
    {
        public SaveGameInfoVM(string? sourceId, ISaveGameInfo info)
        {
            Source = info;
            Label = info switch
            {
                LoadedSaveGameInfo l => l.IsServerSave
                    ? "TODO SERVER"
                    : "TODO NON-SERVER",
                InvalidSaveGameInfo => $"{sourceId!} (Error reading metadata)",
                MissingSaveGameInfo => $"{sourceId!} (Missing metadata)",
                PlaceholderSaveGameInfo p => p.Label,
                _ => throw new NotImplementedException()
            };
        }

        public string Label { get; }

        public ISaveGameInfo Source { get; }
    }

    internal partial class SaveGameVM : BaseVM
    {
        private readonly ISaveGame source;

        private static readonly FeedbackVM InvalidSaveFeedback = new("The save file is invalid and will not be loaded.");
        private static readonly FeedbackVM LoadErrorFeedback = new("An error occurred when loading the save file.");

        private Task<SaveGameDetails?>? detailsTask;
        public Task<SaveGameDetails?> CachedDetails => detailsTask ??= Task.Run(() =>
        {
            if (!source.IsValid)
            {
                Dispatcher.UIThread.Post(() => ValidityFeedback = InvalidSaveFeedback);
                return null;
            }

            // TODO - loading popup

            var cached = StorageManager.Cache.LoadSaveDetails(source);
            if (cached == null || cached.IsOutdated(PalDB.LoadEmbedded()))
            {
                try
                {
                    cached = SaveGameDetails.FromSaveGame(source, PalDB.LoadEmbedded());
                    StorageManager.Cache.StoreSaveDetails(cached);
                    Dispatcher.UIThread.Post(() => ValidityFeedback = FeedbackVM.None);
                }
                catch (Exception e) // TODO - feedback, log
                {
                    cached = null;
                    Dispatcher.UIThread.Post(() => ValidityFeedback = LoadErrorFeedback);
                }
            }
            return cached;
        });

        private SaveGameInfoVM info;
        public SaveGameInfoVM Info
        {
            get => info;
            private set => SetProperty(ref info, value);
        }

        private FeedbackVM validityFeedback;
        public FeedbackVM ValidityFeedback
        {
            get => validityFeedback;
            private set => SetProperty(ref validityFeedback, value);
        }

        // whether the save game is in a usable (successfully loaded) state
        private Task<bool>? canUseTask;
        public Task<bool> CanUse => canUseTask ??= Task.Run(async () => (await CachedDetails) != null);

        // whether the save game is "generally" valid
        public bool IsValid { get; }

        public string Label => info.Label;

        public SaveGameVM(ISaveGame source)
        {
            this.source = source;
            this.info = new SaveGameInfoVM(source.Identifier(), ISaveGameInfo.FromSave(source));

            IsValid = source.IsValid;
            this.validityFeedback = IsValid ? FeedbackVM.None : InvalidSaveFeedback;
        }

        public DateTime LastModified => source.LastModified;
    }
}
