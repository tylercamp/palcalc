using PalCalc.Model;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackTargetContextTests
{
    [TestMethod]
    public void Settings_CapturesZeroFiniteAndUnlimitedCakeLimits()
    {
        Assert.AreEqual(0, SolverTestScenario.Solver([]).Settings.MaxSpecialCakes);
        Assert.AreEqual(12, SolverTestScenario.Solver([], maxSpecialCakes: 12).Settings.MaxSpecialCakes);
        Assert.IsNull(SolverTestScenario.Solver([], maxSpecialCakes: null).Settings.MaxSpecialCakes);
    }

    [TestMethod]
    public void Settings_RejectsNegativeCakeLimit()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            SolverTestScenario.Solver([], maxSpecialCakes: -1)
        );
    }

    [TestMethod]
    public void Context_NormalizesDuplicateAttacksWithoutMutatingCaller()
    {
        var attack = SolverTestScenario.DB.ActiveSkills.First();
        var target = Target(attack, attack);

        var context = new AttackTargetContext(target, SolverTestScenario.DB);

        Assert.AreEqual(2, target.RequiredAttacks.Count);
        Assert.AreEqual(1, context.FullTargetMask);
        Assert.AreSame(attack, context.AttackForBit(1));
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(2, 3)]
    [DataRow(3, 7)]
    [DataRow(4, 15)]
    [DataRow(5, 31)]
    [DataRow(6, 63)]
    public void Context_MapsZeroThroughSixTargetsToFullMask(int count, int expectedMask)
    {
        var context = new AttackTargetContext(
            Target(SolverTestScenario.DB.ActiveSkills.Take(count).ToArray()),
            SolverTestScenario.DB
        );

        Assert.AreEqual((byte)expectedMask, context.FullTargetMask);
        Assert.AreEqual(count != 0, context.IsActive);
    }

    [TestMethod]
    public void Context_RejectsMoreThanSixDistinctTargets()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new AttackTargetContext(
                Target(SolverTestScenario.DB.ActiveSkills.Take(7).ToArray()),
                SolverTestScenario.DB
            )
        );
    }

    [TestMethod]
    public void Context_SeparatesInheritableTargets()
    {
        var nonInheritable = SolverTestScenario.DB.ActiveSkills.First(attack => !attack.CanInherit);
        var inheritable = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit);
        var context = new AttackTargetContext(Target(nonInheritable, inheritable), SolverTestScenario.DB);

        Assert.AreEqual(0b11, context.FullTargetMask);
        Assert.AreEqual(0b10, context.InheritableTargetMask);
        Assert.AreEqual(0b11, context.MaskOf([nonInheritable, inheritable]));
    }

    [TestMethod]
    public void Context_CachesLevel1TargetAndNeutralState()
    {
        var pal = SolverTestScenario.DB.Pals.First(pal =>
            pal.Level1ActiveSkills(SolverTestScenario.DB).Any(attack => !attack.CanInherit)
        );
        var level1Attacks = pal.Level1ActiveSkills(SolverTestScenario.DB).ToArray();
        var context = new AttackTargetContext(Target(level1Attacks[0]), SolverTestScenario.DB);

        var state = context.StateOf(pal);

        Assert.AreEqual(1, state.Level1TargetMask);
        Assert.AreEqual(level1Attacks.Any(attack => !attack.CanInherit), state.HasNeutralLevel1Attack);
        Assert.AreEqual(state, context.StateOf(pal));
    }

    [TestMethod]
    public void SolverRunContext_ExposesContextForTheCapturedTarget()
    {
        var attack = SolverTestScenario.DB.ActiveSkills.First();
        var target = Target(attack, attack);
        var request = new BreedingSolverRequest(target, SolverTestScenario.Solver([]).Settings);

        var runContext = SolverRunContext.Create(
            request,
            new SolverStateController(CancellationToken.None)
        );

        Assert.IsTrue(runContext.AttackTargets.IsActive);
        Assert.AreEqual(1, runContext.AttackTargets.FullTargetMask);
        Assert.AreEqual(2, target.RequiredAttacks.Count);
    }

    private static PalSpecifier Target(params ActiveSkill[] attacks) => new()
    {
        Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
        RequiredAttacks = attacks.ToList(),
    };
}
