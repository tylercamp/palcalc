using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;

namespace PalCalc.UI.ViewModel.Solver
{
    /// <summary>
    /// An entry in the "Tools" section of the solver page. While a tool is selected it replaces
    /// the center pane, using the same Source Pals and solver settings as the target pals.
    ///
    /// To add a tool, implement this, add a `DataTemplate` for it in `SolverPage.xaml`, and add it
    /// to the list in `SolverPageViewModel`.
    /// </summary>
    public interface ISolverTool
    {
        ILocalizedText Name { get; }

        void Run(BreedingSolverSettings settings, CachedSaveGame save);
    }
}
