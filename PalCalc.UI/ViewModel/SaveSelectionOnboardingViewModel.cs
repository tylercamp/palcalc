using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PalCalc.UI.ViewModel
{
    internal partial class SaveSelectionOnboardingViewModel : ObservableObject
    {
        // TODO - Right now Steam is completely hidden if not detected, should instead display with
        //        warning "Steam saves location not found"

        static SaveSelectionOnboardingViewModel()
        {
            List<ISavesLocation> locations = DirectSavesLocation.AllLocal.Cast<ISavesLocation>().ToList();
            var xboxLocations = XboxSavesLocation.FindAll();
            if (xboxLocations.Count > 0)
                locations.AddRange(xboxLocations);
            else
                locations.Add(new XboxSavesLocation());

            DesignerInstance = new SaveSelectionOnboardingViewModel(locations, Enumerable.Empty<ISaveGame>());
        }

        public static SaveSelectionOnboardingViewModel DesignerInstance { get; }

        public delegate void CustomSaveHandler(ManualSavesLocationViewModel location, ISaveGame save);
        public event CustomSaveHandler NewCustomSaveSelected;
        public event CustomSaveHandler CustomSaveDelete;

        public IReadOnlyList<ISavesLocationViewModel> Locations { get; }

        ManualSavesLocationViewModel ManualLocation;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentSaveGames))]
        [NotifyPropertyChangedFor(nameof(SelectedGameIsManual))]
        [NotifyPropertyChangedFor(nameof(CanOpenLocationFolder))]
        ISavesLocationViewModel? _selectedLocation;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedSaveGame))]
        [NotifyPropertyChangedFor(nameof(CanOpenSaveFolder))]
        [NotifyPropertyChangedFor(nameof(SelectedSaveGamePlayerName))]
        [NotifyPropertyChangedFor(nameof(SelectedSaveGamePlayerLevel))]
        [NotifyPropertyChangedFor(nameof(SelectedSaveGameWorldName))]
        [NotifyPropertyChangedFor(nameof(SelectedSaveGameDay))]
        [NotifyPropertyChangedFor(nameof(SelectedSaveGameLastModified))]
        [NotifyPropertyChangedFor(nameof(SelectedSaveGameGameId))]
        [NotifyPropertyChangedFor(nameof(HasSelectedSave))]
        ISaveGameViewModel? _selectedGame;

        public IReadOnlyList<ISaveGameViewModel> CurrentSaveGames =>
            SelectedLocation?.SaveGames.ToList() ?? Enumerable.Empty<ISaveGameViewModel>().ToList();

        public SaveGameViewModel? SelectedSaveGame => SelectedGame as SaveGameViewModel;

        public bool SelectedGameIsManual => SelectedLocation is ManualSavesLocationViewModel;

        public bool CanOpenLocationFolder =>
            (SelectedLocation as StandardSavesLocationViewModel)?.Value?.FolderPath != null;

        public bool CanOpenSaveFolder =>
            SelectedSaveGame?.Value?.BasePath != null;

        public bool ShowXboxInstallHint =>
            SelectedLocation is StandardSavesLocationViewModel { Value: XboxSavesLocation } &&
            (SelectedLocation.SaveGames?.Count ?? 0) == 0;

        public Visibility XboxIncompleteVisibility =>
            SelectedSaveGame != null && SelectedSaveGame.Value is XboxSaveGame xboxSave &&
            xboxSave.LevelMeta?.IsValid != true
                ? Visibility.Visible
                : Visibility.Collapsed;

        public bool HasSelectedSave => SelectedSaveGame != null;

        public string SelectedSaveGamePlayerName => SelectedSaveGame?.Value?.LevelMeta?.ReadGameOptions()?.PlayerName;
        public int SelectedSaveGamePlayerLevel => SelectedSaveGame?.Value?.LevelMeta?.ReadGameOptions()?.PlayerLevel ?? 0;
        public string SelectedSaveGameWorldName => SelectedSaveGame?.Value?.LevelMeta?.ReadGameOptions()?.WorldName;
        public int SelectedSaveGameDay => SelectedSaveGame?.Value?.LevelMeta?.ReadGameOptions()?.InGameDay ?? 0;
        public DateTime SelectedSaveGameLastModified => SelectedSaveGame?.Value?.LastModified ?? default;
        public string SelectedSaveGameGameId => SelectedSaveGame?.Value?.GameId;

        public SaveSelectionOnboardingViewModel(
            IEnumerable<ISavesLocation> savesLocations,
            IEnumerable<ISaveGame> manualSaves)
        {
            ManualLocation = new ManualSavesLocationViewModel(manualSaves);

            var locationVms = savesLocations
                .Select(l => new StandardSavesLocationViewModel(l) as ISavesLocationViewModel)
                .OrderByDescending(vm => vm.LastModified)
                .ToList();
            locationVms.Add(ManualLocation);
            Locations = locationVms;

            SelectedLocation = Locations.OrderByDescending(l => l.LastModified).FirstOrDefault();

            OpenLocationFolderCommand = new RelayCommand(
                () =>
                {
                    var std = (SelectedLocation as StandardSavesLocationViewModel)!;
                    System.Diagnostics.Process.Start("explorer.exe",
                        System.IO.Path.GetFullPath(std.Value!.FolderPath!));
                },
                () => CanOpenLocationFolder);

            OpenSaveFolderCommand = new RelayCommand(
                () => System.Diagnostics.Process.Start("explorer.exe",
                    System.IO.Path.GetFullPath(SelectedSaveGame!.Value!.BasePath!)),
                () => CanOpenSaveFolder);

            DeleteSaveCommand = new RelayCommand(
                () => CustomSaveDelete?.Invoke(ManualLocation, SelectedSaveGame!.Value!),
                () => SelectedGameIsManual && SelectedSaveGame != null);
        }

        public IRelayCommand OpenLocationFolderCommand { get; }
        public IRelayCommand OpenSaveFolderCommand { get; }
        public IRelayCommand DeleteSaveCommand { get; }

        public void TrySelectSaveGame(string saveIdentifier)
        {
            foreach (var loc in Locations)
            {
                foreach (var game in loc.SaveGames.OfType<SaveGameViewModel>().Where(g => g.Value != null))
                {
                    if (CachedSaveGame.IdentifierFor(game.Value) == saveIdentifier)
                    {
                        SelectedLocation = loc;
                        SelectedGame = game;
                        return;
                    }
                }
            }
        }
    }
}
