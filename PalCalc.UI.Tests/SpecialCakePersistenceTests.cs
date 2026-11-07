using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Serialization;
using PalCalc.UI.ViewModel.GraphSharp;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class SpecialCakePersistenceTests
{
    [TestMethod]
    [DataRow(AttackInheritanceMode.Baseline, true)]
    [DataRow(AttackInheritanceMode.Normal, true)]
    [DataRow(AttackInheritanceMode.InheritAll, true)]
    [DataRow(AttackInheritanceMode.InheritAll, false)]
    public void FinalizedOutcomeRoundTripsThroughExistingFields(AttackInheritanceMode mode, bool hasTargetAttacks)
    {
        var db = PalDB.LoadEmbedded();
        var pal = db.Pals.First();
        var attack = db.ActiveSkills.First();
        var settings = new GameSettings();
        var outcome = new BreedingOutcome(
            mode == AttackInheritanceMode.InheritAll ? BreedingMethod.SpecialCake : BreedingMethod.Ordinary,
            0.5f
        );
        OwnedPalReference Parent(PalGender gender) => new(
            new PalInstance
            {
                InstanceId = $"parent-{gender}", OwnerPlayerId = "player", Pal = pal, Gender = gender,
                PassiveSkills = [], ActiveSkills = [attack], EquippedActiveSkills = [attack],
                Location = new PalLocation { Type = LocationType.Palbox, ContainerId = "palbox", Index = 0 },
            },
            [], new IV_Set(), AttackProfile.Inactive
        );
        var reference = new BredPalReference(
            settings, pal, PalGender.WILDCARD, Parent(PalGender.MALE), Parent(PalGender.FEMALE),
            avgRequiredBreedings: 2, [], outcome, new IV_Set(), ivsProbability: 1,
            AttackProfile.Inactive,
            hasTargetAttacks ? new MaterializedAttackInheritance(mode, [attack], [attack], [attack], [attack], 1) : null
        );

        var dto = ResultJsonSerializer.ToDto(reference);
        var restored = (BredPalReference)ResultJsonSerializer.FromDto(dto, db, settings, new SerializableSolverSettings());

        Assert.AreEqual(outcome, restored.Outcome);
        Assert.AreEqual(reference.AvgRequiredBreedings, restored.AvgRequiredBreedings);
        Assert.AreEqual(reference.BreedingEffort, restored.BreedingEffort);
        Assert.AreEqual(reference.SelfSpecialCakes,
            restored.SelfSpecialCakes);
        Assert.AreEqual(reference.ExpectedSpecialCakes, restored.ExpectedSpecialCakes);
        if (hasTargetAttacks)
            Assert.HasCount(1, restored.MaterializedAttackInheritance.InheritedAttacks);
        else
        {
            Assert.IsNull(restored.MaterializedAttackInheritance);
            var display = new BreedingResultViewModel(null, settings, restored, []);
            Assert.AreEqual(2, display.NumSpecialCakes);
            var cakeStep = display.Graph.Nodes.OfType<StandardBreedingTreeNodeViewModel>()
                .Single(node => node.UsesSpecialCake);
            Assert.AreEqual(2, cakeStep.SpecialCakes);
        }
    }
}
