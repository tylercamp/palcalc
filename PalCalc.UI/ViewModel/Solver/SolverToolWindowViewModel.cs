using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace PalCalc.UI.ViewModel.Solver
{
    /// <summary>
    /// The window for one tool, opened from the save's menu on the solver page. The Source Pals are the
    /// same ones the solver page uses, so a change in either place applies to both.
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
