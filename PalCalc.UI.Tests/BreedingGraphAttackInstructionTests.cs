using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Tree;
using PalCalc.UI.ViewModel.GraphSharp;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class BreedingGraphAttackInstructionTests
{
    [TestMethod]
    public void GraphAssignsNormalizedParentLoadoutsAndChildInstructions()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(3).ToArray();
        var pals = db.Pals.OrderBy(pal => pal.InternalIndex).ToArray();
        var result = Bred(pals[0], Owned(pals[0], "first", attacks[0], 1), Owned(pals[^1], "second", attacks[1], 2),
            new(AttackInheritanceMode.InheritAll, [attacks[0]], [attacks[1]], [attacks[0], attacks[1]], [attacks[0], attacks[1], attacks[2]], 7, .25f));

        var graph = BreedingGraph.FromPalReference(null, new GameSettings(), result, []);
        var root = (BredPalNode)graph.Tree.Root;
        var parent1 = (StandardBreedingTreeNodeViewModel)graph.NodeFor(root.ParentNode1);
        var parent2 = (StandardBreedingTreeNodeViewModel)graph.NodeFor(root.ParentNode2);
        var child = (StandardBreedingTreeNodeViewModel)graph.NodeFor(root);

        AssertAttacks(result.MaterializedAttackInheritance!.Parent1Loadout, parent1.EquippedAttacks);
        AssertAttacks(result.MaterializedAttackInheritance.Parent2Loadout, parent2.EquippedAttacks);
        AssertAttacks(result.MaterializedAttackInheritance.ChildMasteredAttacks, child.MasteredAttacks);
        AssertAttacks(result.MaterializedAttackInheritance.InheritedAttacks, child.InheritedAttacks);
        Assert.AreEqual(7, child.SpecialCakes);
        Assert.IsTrue(child.UsesSpecialCake);
    }

    [TestMethod]
    public void IntermediateUsesItsDownstreamLoadoutAndSpecialCakesAreAggregated()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(3).ToArray();
        var inner = Bred(db.Pals.First(), Owned(db.Pals.First(), "inner-first", attacks[0], 1), Owned(db.Pals.Last(), "inner-second", attacks[1], 2),
            new(AttackInheritanceMode.Normal, [attacks[0]], [attacks[1]], [attacks[0]], [attacks[0], attacks[1]], 1, 1));
        var outer = Bred(db.Pals.Last(), inner, Owned(db.Pals.Last(), "outer", attacks[2], 3),
            new(AttackInheritanceMode.InheritAll, [attacks[0]], [attacks[2]], [attacks[0], attacks[2]], [attacks[0], attacks[2]], 9, .25f));

        var display = new BreedingResultViewModel(null, new GameSettings(), outer, [attacks[0], attacks[2]]);
        var intermediate = display.Graph.Nodes.OfType<StandardBreedingTreeNodeViewModel>().Single(node => node.Value.PalRef == inner);

        AssertAttacks(inner.MaterializedAttackInheritance!.ChildMasteredAttacks, intermediate.MasteredAttacks);
        AssertAttacks(
            ReferenceEquals(outer.Parent1, inner)
                ? outer.MaterializedAttackInheritance!.Parent1Loadout
                : outer.MaterializedAttackInheritance!.Parent2Loadout,
            intermediate.EquippedAttacks
        );
        Assert.AreEqual(10, display.NumSpecialCakes);
        AssertAttacks([attacks[0], attacks[2]], display.EffectiveAttacks);
    }

    [TestMethod]
    public void TerminalOwnedPalUsesTargetAttacksAndSurgeryParentReceivesLoadout()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(2).ToArray();
        var owned = Owned(db.Pals.First(), "owned", attacks[0], 1);
        var terminal = new BreedingResultViewModel(null, new GameSettings(), owned, attacks);
        var surgery = new SurgeryTablePalReference(owned, []);
        var result = Bred(db.Pals.Last(), surgery, Owned(db.Pals.Last(), "other", attacks[1], 2),
            new(AttackInheritanceMode.Normal, [attacks[0]], [attacks[1]], [attacks[0]], [attacks[0]], 0, 1));

        var graph = BreedingGraph.FromPalReference(null, new GameSettings(), result, []);
        var root = (BredPalNode)graph.Tree.Root;
        var surgeryResult = (StandardBreedingTreeNodeViewModel)graph.NodeFor(root.ParentNode1);

        AssertAttacks(attacks, terminal.EffectiveAttacks);
        AssertAttacks(attacks, ((StandardBreedingTreeNodeViewModel)terminal.Graph.NodeFor(terminal.Graph.Tree.Root)).EquippedAttacks);
        AssertAttacks(result.MaterializedAttackInheritance!.Parent1Loadout, surgeryResult.EquippedAttacks);
    }

    [TestMethod]
    public void WildPalDisplaysItsLevelOneMasteredAttacks()
    {
        var db = PalDB.LoadEmbedded();
        var pal = db.Pals.First(candidate => candidate.Level1AttackInternalIds.Count > 0);
        var wild = new WildPalReference(
            pal,
            [],
            0,
            db.BreedingMechanics,
            AttackProfile.Inactive,
            hasNeutralAttack: false
        );
        var graph = BreedingGraph.FromPalReference(null, new GameSettings(), wild, []);
        var node = (StandardBreedingTreeNodeViewModel)graph.NodeFor(graph.Tree.Root);

        AssertAttacks(pal.Level1ActiveSkills(db), node.MasteredAttacks);
    }

    [TestMethod]
    public void SpecialCakeColumnCollapsesOnlyWhenEveryResultUsesNone()
    {
        var db = PalDB.LoadEmbedded();
        var attack = db.ActiveSkills.First();
        var withoutCake = new BreedingResultViewModel(null, new GameSettings(), Owned(db.Pals.First(), "owned", attack, 1), []);
        var withCake = new BreedingResultViewModel(null, new GameSettings(), Bred(db.Pals.Last(), Owned(db.Pals.First(), "first", attack, 2), Owned(db.Pals.Last(), "second", attack, 3), new(AttackInheritanceMode.InheritAll, [attack], [attack], [attack], [attack], 1, 1)), []);
        var results = new BreedingResultListViewModel { Results = [withoutCake] };

        Assert.AreEqual(0, results.NumSpecialCakesWidth);

        results.Results = [withoutCake, withCake];

        Assert.IsTrue(double.IsNaN(results.NumSpecialCakesWidth));
    }

    private static BredPalReference Bred(Pal pal, IPalReference parent1, IPalReference parent2, MaterializedAttackInheritance? inheritance) => new(
        new GameSettings(), pal, parent1, parent2, [], 1,
        new IV_Set { HP = IV_Value.Random, Attack = IV_Value.Random, Defense = IV_Value.Random }, 1,
        attackProfile: AttackProfile.Inactive,
        hasNeutralAttack: false,
        materializedAttackInheritance: inheritance,
        avgRequiredBreedings: null,
        gender: PalGender.WILDCARD
    );

    private static OwnedPalReference Owned(Pal pal, string id, ActiveSkill attack, int index) => new(
        new PalInstance { InstanceId = id, Pal = pal, Gender = PalGender.WILDCARD, PassiveSkills = [], ActiveSkills = [attack], EquippedActiveSkills = [attack], Location = new PalLocation { Type = LocationType.Palbox, Index = index } },
        [], new IV_Set { HP = IV_Value.Random, Attack = IV_Value.Random, Defense = IV_Value.Random },
        attackProfile: AttackProfile.Inactive,
        hasNeutralAttack: false
    );

    private static void AssertAttacks(IEnumerable<ActiveSkill> expected, PalCalc.UI.ViewModel.PalDerived.AttackSkillCollectionViewModel actual) =>
        CollectionAssert.AreEqual(expected.Select(attack => attack.InternalName).ToArray(), actual.AsModelEnumerable().Select(attack => attack.InternalName).ToArray());
}
