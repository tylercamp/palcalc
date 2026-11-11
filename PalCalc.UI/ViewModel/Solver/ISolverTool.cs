using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;

namespace PalCalc.UI.ViewModel.Solver
{
    /// <summary>
    /// An entry in the save's menu on the solver page. Each tool opens in its own window beside the
    /// Source Pals, and uses the same Source Pals and solver settings as the target pals.
    ///
    /// To add a tool, implement this, add a `DataTemplate` for it in `SolverToolWindow.xaml`, and add it
    /// to the list in `SolverPageViewModel`.
    /// </summary>
    public interface ISolverTool
    {
        ILocalizedText Name { get; }

        // called each time the tool is opened from the menu, before `Run`. (the window's Run button doesn't call it)
        void OnSelected() { }

        void Run(BreedingSolverSettings settings, CachedSaveGame save);
    }
}
