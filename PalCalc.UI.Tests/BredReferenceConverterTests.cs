using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.Persistence.Serialization;

namespace PalCalc.UI.Tests;

[TestClass]
public class BredReferenceConverterTests
{
    [TestMethod]
    public void NormalMaterializedInheritanceRoundTripsWithExactEffortAndParentLoadouts()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(3).ToArray();
        var pals = db.Pals.OrderBy(pal => pal.InternalIndex).ToArray();
        var original = Bred(
            pals[0],
            Owned(pals[0], "first", attacks[0], 1),
            Owned(pals[^1], "second", attacks[1], 2),
            new MaterializedAttackInheritance(
                AttackInheritanceMode.Normal,
                [attacks[0]],
                [attacks[1]],
                [attacks[0], attacks[1]],
                [attacks[0], attacks[1], attacks[2]],
                SpecialCakes: 0,
                AttackProbability: 0.5f
            ),
            avgRequiredBreedings: 7,
            gender: PalGender.MALE
        );

        var reloaded = RoundTrip(original, db);

        AssertMaterializedEqual(original.MaterializedAttackInheritance!, reloaded.MaterializedAttackInheritance!);
        Assert.AreEqual(original.AvgRequiredBreedings, reloaded.AvgRequiredBreedings);
        Assert.AreEqual(original.NumTotalEggs, reloaded.NumTotalEggs);
        Assert.AreEqual(original.BreedingEffort, reloaded.BreedingEffort);
        Assert.AreEqual(((OwnedPalReference)reloaded.Parent1).UnderlyingInstance.ActiveSkills.Single(), reloaded.MaterializedAttackInheritance.Parent1Loadout.Single());
        Assert.AreEqual(((OwnedPalReference)reloaded.Parent2).UnderlyingInstance.ActiveSkills.Single(), reloaded.MaterializedAttackInheritance.Parent2Loadout.Single());
    }

    [TestMethod]
    public void InheritAllMaterializedInheritancePreservesGenderAndSpecialCakes()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(2).ToArray();
        var original = Bred(
            db.Pals.First(),
            Owned(db.Pals.First(), "first", attacks[0], 1),
            Owned(db.Pals.Last(), "second", attacks[1], 2),
            new MaterializedAttackInheritance(
                AttackInheritanceMode.InheritAll,
                [attacks[0]],
                [attacks[1]],
                [attacks[0], attacks[1]],
                [attacks[0], attacks[1]],
                SpecialCakes: 17,
                AttackProbability: 0.25f
            ),
            avgRequiredBreedings: 17,
            gender: PalGender.FEMALE
        );

        var reloaded = RoundTrip(original, db);

        Assert.AreEqual(PalGender.FEMALE, reloaded.Gender);
        Assert.AreEqual(17, reloaded.AvgRequiredBreedings);
        Assert.AreEqual(17, reloaded.MaterializedAttackInheritance!.SpecialCakes);
        Assert.AreEqual(AttackInheritanceMode.InheritAll, reloaded.MaterializedAttackInheritance.Mode);
    }

    [TestMethod]
    public void MultiGenerationMaterializedInheritanceRoundTripsEveryEdge()
    {
        var db = PalDB.LoadEmbedded();
        var attacks = db.ActiveSkills.Take(3).ToArray();
        var intermediate = Bred(
            db.Pals.First(),
            Owned(db.Pals.First(), "inner-first", attacks[0], 1),
            Owned(db.Pals.Last(), "inner-second", attacks[1], 2),
            new MaterializedAttackInheritance(AttackInheritanceMode.Normal, [attacks[0]], [attacks[1]], [attacks[0]], [attacks[0]], 1, 1),
            avgRequiredBreedings: 2
        );
        var original = Bred(
            db.Pals.Last(),
            intermediate,
            Owned(db.Pals.Last(), "outer", attacks[2], 3),
            new MaterializedAttackInheritance(AttackInheritanceMode.InheritAll, [attacks[0]], [attacks[2]], [attacks[0], attacks[2]], [attacks[0], attacks[2]], 9, 0.25f),
            avgRequiredBreedings: 9
        );

        var reloaded = RoundTrip(original, db);
        var reloadedEdges = BredReferences(reloaded).OrderBy(reference => reference.MaterializedAttackInheritance!.SpecialCakes).ToArray();
        var originalEdges = BredReferences(original).OrderBy(reference => reference.MaterializedAttackInheritance!.SpecialCakes).ToArray();

        Assert.HasCount(2, reloadedEdges);
        for (var index = 0; index < originalEdges.Length; index++)
            AssertMaterializedEqual(originalEdges[index].MaterializedAttackInheritance!, reloadedEdges[index].MaterializedAttackInheritance!);
    }

    private static BredPalReference Bred(
        Pal pal,
        IPalReference parent1,
        IPalReference parent2,
        MaterializedAttackInheritance inheritance,
        int avgRequiredBreedings,
        PalGender gender = PalGender.WILDCARD
    ) => new(
        new GameSettings(),
        pal,
        parent1,
        parent2,
        [],
        passivesProbability: 0.5f,
        new IV_Set { HP = IV_Value.Random, Attack = IV_Value.Random, Defense = IV_Value.Random },
        ivsProbability: 0.5f,
        attackProfile: AttackProfile.Inactive,
        materializedAttackInheritance: inheritance,
        avgRequiredBreedings: avgRequiredBreedings,
        gender: gender
    );

    private static OwnedPalReference Owned(Pal pal, string id, ActiveSkill attack, int index) => new(
        new PalInstance
        {
            InstanceId = id,
            Pal = pal,
            Gender = PalGender.WILDCARD,
            PassiveSkills = [],
            ActiveSkills = [attack],
            EquippedActiveSkills = [attack],
            Location = new PalLocation { Type = LocationType.Palbox, Index = index },
        },
        [],
        new IV_Set { HP = IV_Value.Random, Attack = IV_Value.Random, Defense = IV_Value.Random },
        attackProfile: AttackProfile.Inactive
    );

    private static IEnumerable<BredPalReference> BredReferences(IPalReference reference)
    {
        if (reference is not BredPalReference bred)
            yield break;

        yield return bred;
        foreach (var parent in BredReferences(bred.Parent1).Concat(BredReferences(bred.Parent2)))
            yield return parent;
    }

    private static BredPalReference RoundTrip(BredPalReference reference, PalDB db)
    {
        var json = JsonConvert.SerializeObject(ResultJsonSerializer.ToDto(reference));
        var dto = JsonConvert.DeserializeObject<PalReferenceDto>(json)!;
        return (BredPalReference)ResultJsonSerializer.FromDto(
            dto,
            db,
            new GameSettings(),
            new SerializableSolverSettings()
        );
    }

    private static void AssertMaterializedEqual(MaterializedAttackInheritance expected, MaterializedAttackInheritance actual)
    {
        Assert.AreEqual(expected.Mode, actual.Mode);
        CollectionAssert.AreEqual(expected.Parent1Loadout.Select(attack => attack.InternalName).ToArray(), actual.Parent1Loadout.Select(attack => attack.InternalName).ToArray());
        CollectionAssert.AreEqual(expected.Parent2Loadout.Select(attack => attack.InternalName).ToArray(), actual.Parent2Loadout.Select(attack => attack.InternalName).ToArray());
        CollectionAssert.AreEqual(expected.InheritedAttacks.Select(attack => attack.InternalName).ToArray(), actual.InheritedAttacks.Select(attack => attack.InternalName).ToArray());
        CollectionAssert.AreEqual(expected.ChildLearnedAttacks.Select(attack => attack.InternalName).ToArray(), actual.ChildLearnedAttacks.Select(attack => attack.InternalName).ToArray());
        Assert.AreEqual(expected.SpecialCakes, actual.SpecialCakes);
        Assert.AreEqual(expected.AttackProbability, actual.AttackProbability);
    }
}
