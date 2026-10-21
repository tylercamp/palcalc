using PalCalc.Model;
using Newtonsoft.Json.Linq;

namespace PalCalc.Solver.Tests;

[TestClass]
public class BreedingMechanicsTests
{
    private static BreedingMechanics mechanics = PalDB.LoadEmbedded().BreedingMechanics;

    // (sanity check that the weights are scraped and probabilities calculated directly for default game settings)

    [TestMethod]
    public void IVInheritanceWeights_PreserveStoredSelectorWeights()
    {
        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, mechanics.IVInheritanceWeights);
    }

    [TestMethod]
    public void IVInheritanceWeights_RoundTripAsAnOrderedJsonArray()
    {
        var original = new BreedingMechanics([1, 4, 7], new() { [1] = 1 }, new() { [0] = 1 });
        var json = JObject.FromObject(original);
        Assert.IsInstanceOfType<JArray>(json[nameof(BreedingMechanics.IVInheritanceWeights)]);
        var restored = json.ToObject<BreedingMechanics>()!;

        CollectionAssert.AreEqual(new[] { 1, 4, 7 }, restored.IVInheritanceWeights);
        Assert.AreEqual(original.ProbabilityOfInheritingDesiredIVs(true, true, true),
            restored.ProbabilityOfInheritingDesiredIVs(true, true, true));
    }

    [TestMethod]
    public void PassiveProbabilityDirect_MatchesDataminedWeights()
    {
        Assert.AreEqual(0f,    mechanics.PassiveProbabilityDirect[0]);
        Assert.AreEqual(0.40f, mechanics.PassiveProbabilityDirect[1]);
        Assert.AreEqual(0.30f, mechanics.PassiveProbabilityDirect[2]);
        Assert.AreEqual(0.20f, mechanics.PassiveProbabilityDirect[3]);
        Assert.AreEqual(0.10f, mechanics.PassiveProbabilityDirect[4]);
    }

    [TestMethod]
    public void PassiveRandomProbability_MatchesDataminedWeights()
    {
        Assert.AreEqual(0.40f, mechanics.PassiveRandomAddedProbability[0]);
        Assert.AreEqual(0.30f, mechanics.PassiveRandomAddedProbability[1]);
        Assert.AreEqual(0.20f, mechanics.PassiveRandomAddedProbability[2]);
        Assert.AreEqual(0.10f, mechanics.PassiveRandomAddedProbability[3]);
        Assert.AreEqual(0f,    mechanics.PassiveRandomAddedProbability[4]);
    }
}
