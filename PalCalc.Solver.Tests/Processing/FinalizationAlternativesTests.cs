using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class FinalizationAlternativesTests
{
    [TestMethod]
    public void KeepsBothExtremesAndCompromiseButRemovesDominatedAlternative()
    {
        var source = Owned();
        var alternatives = new FinalizationAlternatives<int>();
        alternatives.Add(source, new(TimeSpan.FromHours(110), 12), 12);
        alternatives.Add(source, new(TimeSpan.FromHours(146), 8), 8);
        alternatives.Add(source, new(TimeSpan.FromHours(80), 15), 15);
        alternatives.Add(source, new(TimeSpan.FromHours(95), 10), 10);

        CollectionAssert.AreEquivalent(new[] { 8, 10, 15 },
            alternatives.Results.ToArray());
        Assert.IsTrue(alternatives.DominatesLowerBounds(source, new(TimeSpan.FromHours(110), 12)));
        Assert.IsFalse(alternatives.DominatesLowerBounds(source, new(TimeSpan.FromHours(70), 20)));
    }

    [TestMethod]
    public void EqualMetricsPreserveDifferentPaths()
    {
        var source = Owned();
        var alternatives = new FinalizationAlternatives<int>();
        alternatives.Add(source, new(TimeSpan.FromHours(80), 10), 10);
        alternatives.Add(source, new(TimeSpan.FromHours(80), 10), 10);
        Assert.AreEqual(2, alternatives.Results.Count());
        Assert.IsFalse(alternatives.DominatesLowerBounds(source, new(TimeSpan.FromHours(80), 10)));
    }

    [TestMethod]
    public void DifferentInputsOutputsAndResourceRequirementsRemainComparableOnlyWithinTheirCategory()
    {
        var source = Owned();
        var differentInput = Owned();
        var differentIVs = new OwnedPalReference(source.UnderlyingInstance, [],
            new IV_Set(new(true, 100, 100), IV_Value.Random, IV_Value.Random), source.AttackProfile);
        var passive = SolverTestScenario.DB.SurgeryPassiveSkills.First();
        var surgery = new SurgeryTablePalReference(source, [new AddPassiveSurgeryOperation(passive)]);
        var training = new OwnedPalReference(source.UnderlyingInstance, [], source.IVs,
            source.AttackProfile, new LevelRequirements(1, 50));
        var references = new IPalReference[] { source, differentInput, differentIVs, surgery, training };
        var alternatives = new FinalizationAlternatives<int>();
        for (var i = 0; i < references.Length; i++)
            alternatives.Add(references[i], new(TimeSpan.FromHours(80 + i), 10 + i), 10 + i);

        Assert.AreEqual(references.Length, alternatives.Results.Count());
    }

    private static OwnedPalReference Owned() => new(
        SolverTestScenario.Owned("Katress", PalGender.MALE), [], new IV_Set(),
        new AttackProfile((byte)1));
}
