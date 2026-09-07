using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.Tests.PalReference;

[TestClass]
public class SurgeryTablePalReferenceTests
{
    [TestMethod]
    public void ReplacePassive_RemovesThePassiveFromEachIndependentOrdering()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var runner = "Runner".ToStandardPassive(SolverTestScenario.DB);
        var artisan = "Artisan".ToStandardPassive(SolverTestScenario.DB);
        var serious = "Serious".ToStandardPassive(SolverTestScenario.DB);
        var workSlave = "Work Slave".ToStandardPassive(SolverTestScenario.DB);
        var owned = SolverTestScenario.Owned(
            "Katress",
            PalGender.MALE,
            [runner, swift, artisan, serious]
        );
        var input = new OwnedPalReference(
            owned,
            effectivePassives: [swift, runner, artisan, serious],
            effectiveIVs: new IV_Set(),
            attackProfile: AttackProfile.Inactive
        );

        var result = new SurgeryTablePalReference(
            input,
            [new ReplacePassiveSurgeryOperation(swift, workSlave)]
        );

        CollectionAssert.AreEqual(
            new[] { runner, artisan, serious, workSlave },
            result.ActualPassives
        );
        CollectionAssert.AreEqual(
            new[] { runner, artisan, serious, workSlave },
            result.EffectivePassives
        );
    }
}
