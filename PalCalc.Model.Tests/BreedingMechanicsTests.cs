using PalCalc.Model;

namespace PalCalc.Solver.Tests;

[TestClass]
public class BreedingMechanicsTests
{
    private static BreedingMechanics mechanics = PalDB.LoadEmbedded().BreedingMechanics;

    // (sanity check that the weights are scraped and probabilities calculated directly for default game settings)

    [TestMethod]
    public void IVProbabilityDirect_MatchesDataminedWeights()
    {
        Assert.AreEqual(0f,      mechanics.IVProbabilityDirect[0]);
        Assert.AreEqual(3f / 6f, mechanics.IVProbabilityDirect[1]);
        Assert.AreEqual(2f / 6f, mechanics.IVProbabilityDirect[2]);
        Assert.AreEqual(1f / 6f, mechanics.IVProbabilityDirect[3]);
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
