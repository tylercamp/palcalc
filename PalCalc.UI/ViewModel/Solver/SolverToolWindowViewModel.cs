using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    /// <summary>
    /// A tool window starts with a copy of the solver page's settings and Source Pals.
    /// </summary>
    public partial class SolverToolWindowViewModel(
        ISolverTool tool,
        SolverControlsViewModel initialSolverControls,
        GameSettingsViewModel initialGameSettings,
        Func<CachedSaveGame> getSave
    ) : ObservableObject
    {
        public ISolverTool Tool { get; } = tool;

        public SolverControlsViewModel SolverControls { get; } = initialSolverControls.Copy();
        public PalSourceViewModel SourcePals => SolverControls.SourcePals;
        private readonly GameSettings gameSettingsSnapshot = initialGameSettings.ModelObject;

        [RelayCommand]
        internal void Run()
        {
            var save = getSave();
            if (save == null) return;

            var settings = SolverControls.ConfiguredSolverSettings(gameSettingsSnapshot, SourcePals?.AvailablePals.ToList() ?? []);
            Tool.Run(settings, save);
        }
    }
}
