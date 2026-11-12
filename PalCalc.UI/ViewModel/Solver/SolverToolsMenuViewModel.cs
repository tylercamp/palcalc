using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;

namespace PalCalc.UI.ViewModel.Solver
{
    /// <summary>
    /// The app's Tools menu. It lists the solver page's tools while a save is open there, and is hidden otherwise.
    /// </summary>
    public partial class SolverToolsMenuViewModel : ObservableObject
    {
        private Action<ISolverTool> openTool;

        [ObservableProperty]
        private IReadOnlyList<ISolverTool> tools = [];

        public void SetTools(IReadOnlyList<ISolverTool> tools, Action<ISolverTool> openTool)
        {
            Tools = tools ?? [];
            this.openTool = openTool;
        }

        [RelayCommand]
        private void OpenTool(ISolverTool tool) => openTool?.Invoke(tool);
    }
}
