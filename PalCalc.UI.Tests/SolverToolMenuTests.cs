using CommunityToolkit.Mvvm.Input;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

// the tools open from the save's menu, see `CommonSaveOperationsViewModel`
// (the tool view models use localization, which expects to run on the UI thread)
[TestClass]
[DoNotParallelize]
public class SolverToolMenuTests
{
    [TestMethod]
    public void Tools_EmptyByDefault()
    {
        Assert.AreEqual(0, new CommonSaveOperationsViewModel(null, null, null).Tools.Count);
    }

    [TestMethod]
    public void OpenTool_ClosesMenuAndOpensTool()
    {
        ISolverTool opened = null;
        var tool = new BreedableToolViewModel(null!);
        var operations = new CommonSaveOperationsViewModel(null, null, null).WithTools([tool], t => opened = t);
        operations.MenuIsOpen = true;

        operations.OpenToolCommand.Execute(tool);

        Assert.IsFalse(operations.MenuIsOpen);
        Assert.AreSame(tool, opened);
    }

    [TestMethod]
    public void WithTools_KeepsNavigateCommand()
    {
        var navigate = new RelayCommand(() => { });
        var tool = new BreedableToolViewModel(null!);

        var operations = new CommonSaveOperationsViewModel(navigate, null, null)
            .WithNavigateCondition(() => true)
            .WithTools([tool], _ => { });

        Assert.IsNotNull(operations.NavigateSaveSelectionPageCommand);
        CollectionAssert.AreEqual(new[] { tool }, operations.Tools.ToList());
    }

    [TestMethod]
    public void WithNavigateCondition_KeepsTools()
    {
        ISolverTool opened = null;
        var tool = new BreedableToolViewModel(null!);

        var operations = new CommonSaveOperationsViewModel(new RelayCommand(() => { }), null, null)
            .WithTools([tool], t => opened = t)
            .WithNavigateCondition(() => true);

        operations.OpenToolCommand.Execute(tool);

        CollectionAssert.AreEqual(new[] { tool }, operations.Tools.ToList());
        Assert.AreSame(tool, opened);
    }
}
