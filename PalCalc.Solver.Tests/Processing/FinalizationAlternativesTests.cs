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

    [TestMethod]
    public void ComparisonMatchesPassiveSetsRegardlessOfOrder()
    {
        var source = Owned();
        var passives = SolverTestScenario.DB.SurgeryPassiveSkills.Take(2).ToArray();
        var first = new OwnedPalReference(source.UnderlyingInstance, [passives[0], passives[1]], source.IVs, source.AttackProfile);
        var reordered = new OwnedPalReference(source.UnderlyingInstance, [passives[1], passives[0]], source.IVs, source.AttackProfile);
        var different = new OwnedPalReference(source.UnderlyingInstance, [passives[0]], source.IVs, source.AttackProfile);
        var comparison = new FinalizationComparison();

        Assert.AreEqual(comparison.KeyOf(first), comparison.KeyOf(reordered));
        Assert.AreNotEqual(comparison.KeyOf(first), comparison.KeyOf(different));
    }

    [TestMethod]
    public void ComparisonSortsParentInputsAndKeepsRepeatedOccurrences()
    {
        var first = Owned();
        var second = Owned();
        var third = Owned();
        var comparison = new FinalizationComparison();
        comparison.KeyOf(first);
        comparison.KeyOf(second);
        comparison.KeyOf(third);

        var root = Breed(Breed(first, third), Breed(first, second));

        Assert.AreEqual("0,0,1,2", comparison.KeyOf(root).Inputs);

        BredPalReference Breed(IPalReference left, IPalReference right) => new(
            GameSettings.Defaults, first.Pal, PalGender.WILDCARD, left, right, 1, [],
            new(BreedingMethod.Ordinary, 1), first.IVs, 1, first.AttackProfile, null, null);
    }

    private static OwnedPalReference Owned() => new(
        SolverTestScenario.Owned("Katress", PalGender.MALE), [], new IV_Set(),
        new AttackProfile((byte)1));
}
