using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

// the tools open from the app's Tools menu, see `SolverToolsMenuViewModel`
// (the tool view models use localization, which expects to run on the UI thread)
[TestClass]
[DoNotParallelize]
public class SolverToolMenuTests
{
    [TestMethod]
    public void Tools_EmptyByDefault()
    {
        Assert.AreEqual(0, new SolverToolsMenuViewModel().Tools.Count);
    }

    [TestMethod]
    public void OpenTool_OpensTool()
    {
        ISolverTool opened = null;
        var tool = new BreedableToolViewModel(null!);
        var menu = new SolverToolsMenuViewModel();
        menu.SetTools([tool], t => opened = t);

        menu.OpenToolCommand.Execute(tool);

        CollectionAssert.AreEqual(new[] { tool }, menu.Tools.ToList());
        Assert.AreSame(tool, opened);
    }

    [TestMethod]
    public void SetTools_Null_ClearsTools()
    {
        ISolverTool opened = null;
        var tool = new BreedableToolViewModel(null!);
        var menu = new SolverToolsMenuViewModel();
        menu.SetTools([tool], t => opened = t);

        menu.SetTools(null, null);
        menu.OpenToolCommand.Execute(tool);

        Assert.AreEqual(0, menu.Tools.Count);
        Assert.IsNull(opened);
    }

    [TestMethod]
    public void SetTools_NotifiesTheMenu()
    {
        var changed = new List<string>();
        var menu = new SolverToolsMenuViewModel();
        menu.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        menu.SetTools([new BreedableToolViewModel(null!)], _ => { });

        CollectionAssert.Contains(changed, nameof(SolverToolsMenuViewModel.Tools));
    }
}
