using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.Tests.PalReference;

[TestClass]
public class RandomActiveSkillTests
{
    [TestMethod]
    public void InstancesAreDistinctInheritableSkillsWithStableGroupingName()
    {
        var first = new RandomActiveSkill();
        var second = new RandomActiveSkill();
        var firstHash = first.GetHashCode();

        Assert.IsTrue(first.CanInherit);
        Assert.AreEqual(first.InternalName, second.InternalName);
        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(firstHash, second.GetHashCode());
        Assert.AreEqual(firstHash, first.GetHashCode());
    }
}
