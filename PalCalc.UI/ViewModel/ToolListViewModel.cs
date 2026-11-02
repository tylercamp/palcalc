using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.UI.ViewModel.Solver;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel
{
    public partial class ToolListViewModel(IEnumerable<ISolverTool> tools) : ObservableObject
    {
        public IReadOnlyList<ISolverTool> Tools { get; } = tools.ToList();

        [ObservableProperty]
        private ISolverTool selectedTool;
    }
}
