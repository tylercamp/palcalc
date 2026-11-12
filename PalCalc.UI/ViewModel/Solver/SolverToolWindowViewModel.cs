using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace PalCalc.UI.ViewModel.Solver
{
    /// <summary>
    /// The window for one tool, opened from the app's Tools menu while a save is open. The Source Pals are a copy of
    /// the solver page's, made when the window opens, so changing them here only changes what the tool uses.
    /// </summary>
    public partial class SolverToolWindowViewModel(ISolverTool tool, PalSourceViewModel sourcePals, Action run) : ObservableObject
    {
        public ISolverTool Tool { get; } = tool;

        public PalSourceViewModel SourcePals { get; } = sourcePals;

        // (the solver page builds the settings, so a run always uses the current Source Pals and solver settings)
        [RelayCommand]
        private void Run() => run();
    }
}
