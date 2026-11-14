using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class SolverToolWindowTests
{
    [TestMethod]
    public void EachWindowCopiesCurrentSolverSettings()
    {
        var main = new SolverControlsViewModel(null, null, null, null, null)
        {
            UseGenderReversers = true,
            MaxWildPals = 3,
            BannedBredPals = [PalDB.LoadEmbedded().Pals.First()],
        };
        var gameSettings = new GameSettingsViewModel { BreedingTimeSeconds = 120 };
        var save = new CachedSaveGame(null);
        var breedable = new RecordingTool();
        var combos = new RecordingTool();
        var first = new SolverToolWindowViewModel(breedable, main, gameSettings, () => save);
        var second = new SolverToolWindowViewModel(combos, main, gameSettings, () => save);

        Assert.IsTrue(first.SolverControls.UseGenderReversers);
        Assert.IsTrue(second.SolverControls.UseGenderReversers);
        Assert.AreEqual(3, first.SolverControls.MaxWildPals);
        CollectionAssert.AreEqual(main.BannedBredPals, first.SolverControls.BannedBredPals);

        first.SolverControls.UseGenderReversers = false;
        first.SolverControls.BannedBredPals.Clear();
        Assert.IsTrue(main.UseGenderReversers);
        Assert.IsTrue(second.SolverControls.UseGenderReversers);
        Assert.HasCount(1, main.BannedBredPals);
        Assert.HasCount(1, second.SolverControls.BannedBredPals);

        first.RunCommand.Execute(null);
        Assert.IsFalse(breedable.LastSettings!.UseGenderReversers);

        main.UseGenderReversers = false;
        gameSettings.BreedingTimeSeconds = 240;
        Assert.IsTrue(second.SolverControls.UseGenderReversers);
        second.RunCommand.Execute(null);
        Assert.IsTrue(combos.LastSettings!.UseGenderReversers);
        Assert.AreEqual(TimeSpan.FromSeconds(120), combos.LastSettings.GameSettings.BreedingTime);

        var reopened = new SolverToolWindowViewModel(breedable, main, gameSettings, () => save);
        Assert.IsFalse(reopened.SolverControls.UseGenderReversers);
        Assert.HasCount(1, reopened.SolverControls.BannedBredPals);
        reopened.RunCommand.Execute(null);
        Assert.IsFalse(breedable.LastSettings!.UseGenderReversers);
        Assert.AreEqual(TimeSpan.FromSeconds(240), breedable.LastSettings.GameSettings.BreedingTime);
    }

    private sealed class RecordingTool : ISolverTool
    {
        public ILocalizedText Name => throw new NotSupportedException();
        public BreedingSolverSettings? LastSettings { get; private set; }
        public void Run(BreedingSolverSettings settings, CachedSaveGame save) => LastSettings = settings;
        public void Clear() => LastSettings = null;
    }
}
