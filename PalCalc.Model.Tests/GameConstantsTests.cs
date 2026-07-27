namespace PalCalc.Model.Tests
{
    [TestClass]
    public class GameConstantsTests
    {
        // Pins the inheritance distributions computed from the datamined weight arrays.
        // Combi_TalentInheritNum = [3,2,1]; Combi_PassiveInheritNum = Combi_PassiveRandomAddNum = [4,3,2,1].
        private const float Tol = 0.0001f;

        [TestMethod]
        public void IVProbabilityDirect_MatchesDataminedWeights()
        {
            // [3,2,1] -> 50% / 33.3% / 16.7% (NOT the old hand-typed 50/25/25)
            Assert.AreEqual(3f / 6f, GameConstants.IVProbabilityDirect[1], Tol);
            Assert.AreEqual(2f / 6f, GameConstants.IVProbabilityDirect[2], Tol);
            Assert.AreEqual(1f / 6f, GameConstants.IVProbabilityDirect[3], Tol);
        }

        [TestMethod]
        public void PassiveProbabilityDirect_MatchesDataminedWeights()
        {
            // [4,3,2,1] -> 40 / 30 / 20 / 10
            Assert.AreEqual(0.40f, GameConstants.PassiveProbabilityDirect[1], Tol);
            Assert.AreEqual(0.30f, GameConstants.PassiveProbabilityDirect[2], Tol);
            Assert.AreEqual(0.20f, GameConstants.PassiveProbabilityDirect[3], Tol);
            Assert.AreEqual(0.10f, GameConstants.PassiveProbabilityDirect[4], Tol);
        }

        [TestMethod]
        public void CumulativeTables_AreReverseCumulativeOfExact()
        {
            Assert.AreEqual(1.00f, GameConstants.PassiveProbabilityAtLeastN[1], Tol);
            Assert.AreEqual(0.60f, GameConstants.PassiveProbabilityAtLeastN[2], Tol);
            Assert.AreEqual(0.30f, GameConstants.PassiveProbabilityAtLeastN[3], Tol);
            Assert.AreEqual(0.10f, GameConstants.PassiveProbabilityAtLeastN[4], Tol);

            Assert.AreEqual(1.00f, GameConstants.PassiveRandomAddedAtLeastN[0], Tol);
            Assert.AreEqual(0.60f, GameConstants.PassiveRandomAddedAtLeastN[1], Tol);
        }
    }
}
