using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackResultMaterializerTests
{
    private static readonly Pal Child = "Wixen Noct".ToPal(SolverTestScenario.DB);
    private static readonly ActiveSkill TargetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
        attack.CanInherit && !Child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack));

    [TestMethod]
    public void Materialize_RecomputesEffortAndCakesFromTheConcreteChoice()
    {
        var selectedEntry = Entry(mask: 1, cakes: 0);
        var root = Bred(
            Leaf(1),
            Leaf(0),
            new AttackProfile(selectedEntry)
        );

        var result = Materialize(root, selectedEntry);

        Assert.AreEqual(0.5f, result.MaterializedAttackInheritance.AttackProbability);
        Assert.AreEqual(2, result.AvgRequiredBreedings);
        Assert.AreEqual(0, result.MaterializedAttackInheritance.SpecialCakes);
        Assert.AreEqual(0, result.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.IsTrue(result.BreedingEffort > TimeSpan.Zero);
        Assert.IsFalse(result.MaterializedAttackInheritance.Mode == AttackInheritanceMode.InheritAll);
    }

    [DataTestMethod]
    [DataRow(false, false, 0.5f)]
    [DataRow(true, false, 1f)]
    [DataRow(false, true, 1f)]
    public void Materialize_NormalInheritanceUsesExactProbability(
        bool parent2HasTarget,
        bool parent2HasNoop,
        float expectedProbability
    )
    {
        var parent2 = Leaf(parent2HasTarget ? (byte)1 : (byte)0, parent2HasNoop);
        var entry = Entry(1, cakes: 0);
        var result = Materialize(
            Bred(Leaf(1), parent2, new AttackProfile(entry)),
            entry
        );

        Assert.AreEqual(expectedProbability, result.MaterializedAttackInheritance.AttackProbability);
        Assert.AreEqual(
            (int)Math.Ceiling(1f / expectedProbability),
            result.AvgRequiredBreedings
        );
    }

    [TestMethod]
    public void Materialize_AppliesGenderAdjustedCakeUseOnlyAfterSearchMatching()
    {
        var child = SolverTestScenario.DB.Pals.First(p =>
            SolverTestScenario.DB.BreedingGenderProbability[p][PalGender.MALE] < 0.5f &&
            SolverTestScenario.DB.ActiveSkills.Any(attack =>
                attack.CanInherit && !p.Level1AttackInternalIds.Contains(attack.InternalName)
            )
        );
        var targetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !child.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var context = new AttackTargetContext(
            new PalSpecifier { RequiredAttacks = [targetAttack] },
            SolverTestScenario.DB
        );
        var settings = Settings();
        var selectedEntry = Entry(mask: 1, cakes: 1);
        var root = Bred(
            Leaf(targetAttack, 1),
            Leaf(targetAttack, 0),
            new AttackProfile(selectedEntry),
            child,
            PalGender.MALE,
            settings
        );

        var result = (BredPalReference)new AttackResultMaterializer(context, settings)
            .Materialize(root, selectedEntry);
        var expectedBreedings = BredPalReferenceEffort.WithGuaranteedGender(
            1,
            child,
            SolverTestScenario.DB,
            PalGender.MALE,
            useReverser: false
        );

        Assert.IsTrue(expectedBreedings > 1);
        Assert.AreEqual(expectedBreedings, result.AvgRequiredBreedings);
        Assert.AreEqual(expectedBreedings, result.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.AreEqual(expectedBreedings, result.MaterializedAttackInheritance.SpecialCakes);
    }

    [TestMethod]
    public void Materialize_RecursivelyCalculatesParentAndChildEffort()
    {
        var intermediateEntry = Entry(mask: 1, cakes: 0);
        var intermediate = Bred(
            Leaf(1),
            Leaf(0),
            new AttackProfile(intermediateEntry)
        );
        var rootEntry = Entry(mask: 1, cakes: 0);

        var result = Materialize(
            Bred(intermediate, Leaf(0), new AttackProfile(rootEntry)),
            rootEntry
        );
        var materializedIntermediate = new[] { result.Parent1, result.Parent2 }
            .OfType<BredPalReference>()
            .Single();

        Assert.AreEqual(
            BredPalReferenceEffort.CombineParentEffort(
                Settings().GameSettings,
                result.Parent1,
                result.Parent2,
                result.Parent1.BreedingEffort,
                result.Parent2.BreedingEffort
            ) + BredPalReferenceEffort.CalculateSelfBreedingEffort(
                Settings().GameSettings,
                result.Pal,
                result.Parent1.TimeFactor,
                result.Parent2.TimeFactor,
                result.AvgRequiredBreedings
            ),
            result.BreedingEffort
        );
    }

    [TestMethod]
    public void Materialize_PropagatesRecursiveCakeTotalsExplicitly()
    {
        var intermediateEntry = Entry(mask: 1, cakes: 2);
        var intermediate = Bred(
            Leaf(1, cakes: 1),
            Leaf(0),
            new AttackProfile(intermediateEntry)
        );
        var rootEntry = Entry(mask: 1, cakes: 3);
        var root = Bred(
            intermediate,
            Leaf(0),
            new AttackProfile(rootEntry)
        );

        var result = Materialize(root, rootEntry);
        var materializedIntermediate = new[] { result.Parent1, result.Parent2 }
            .OfType<BredPalReference>()
            .Single();

        Assert.AreEqual(2, materializedIntermediate.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.AreEqual(3, result.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.AreEqual(1, result.MaterializedAttackInheritance.SpecialCakes);
    }

    [TestMethod]
    public void Materialize_ChoosesLowerActualEffortForEqualSearchWitnesses()
    {
        var selectedEntry = Entry(mask: 1, cakes: 0);
        var parent2 = Leaf(0);
        var parent2WithTarget = Leaf(1);
        var parent2Profile = new OwnedPalReference(
            parent2WithTarget.UnderlyingInstance,
            [],
            new IV_Set(),
            new AttackProfile(
                Entry(0),
                Entry(1)
            )
        );
        var root = Bred(
            Leaf(1),
            parent2Profile,
            new AttackProfile(selectedEntry)
        );

        var result = Materialize(root, selectedEntry);

        Assert.AreEqual(1f, result.MaterializedAttackInheritance.AttackProbability);
        Assert.AreEqual(1, result.AvgRequiredBreedings);
        Assert.AreEqual(
            1,
            result.MaterializedAttackInheritance.Parent2Loadout.Count
        );
        Assert.IsTrue(result.MaterializedAttackInheritance.Parent2Loadout.Contains(TargetAttack));
    }

    [TestMethod]
    public void Materialize_ChoosesLowerActualCakeTotalForEqualSearchWitnesses()
    {
        var child = SolverTestScenario.DB.Pals.First(p =>
            SolverTestScenario.DB.BreedingGenderProbability[p][PalGender.MALE] < 0.5f &&
            SolverTestScenario.DB.ActiveSkills.Any(attack =>
                attack.CanInherit && !p.Level1AttackInternalIds.Contains(attack.InternalName)
            )
        );
        var targetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !child.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var context = new AttackTargetContext(
            new PalSpecifier { Pal = child, RequiredAttacks = [targetAttack] },
            SolverTestScenario.DB
        );
        var settings = Settings();
        var searchProfile = new AttackProfile(
            Entry(0),
            Entry(1, cakes: 1)
        );
        var genderAdjustedParent = Bred(
            Leaf(targetAttack, 1),
            Leaf(targetAttack, 0),
            searchProfile,
            child,
            PalGender.MALE,
            settings
        );
        var ordinaryParent = Bred(
            Leaf(targetAttack, 1),
            Leaf(targetAttack, 0),
            searchProfile,
            child,
            PalGender.WILDCARD,
            settings
        );
        var selectedEntry = Entry(1, cakes: 2);
        var root = Bred(
            genderAdjustedParent,
            ordinaryParent,
            new AttackProfile(selectedEntry),
            child,
            settings: settings
        );

        var result = (BredPalReference)new AttackResultMaterializer(context, settings)
            .Materialize(root, selectedEntry);

        Assert.AreEqual(2, result.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.AreEqual(1, result.MaterializedAttackInheritance.SpecialCakes);
    }

    [TestMethod]
    public void Materialize_UsesReferenceIdentityForMemoization()
    {
        var instance = SolverTestScenario.Owned("Katress", PalGender.MALE);
        var entry = Entry(0);
        var first = new OwnedPalReference(
            instance,
            [],
            new IV_Set(),
            new AttackProfile(entry)
        );
        var second = new OwnedPalReference(
            instance,
            [],
            new IV_Set(),
            new AttackProfile(true, entry)
        );
        var materializer = new AttackResultMaterializer(Context(), Settings());

        Assert.AreSame(first, materializer.Materialize(first, entry));
        Assert.AreSame(second, materializer.Materialize(second, entry));
    }

    [TestMethod]
    public void Materialize_ThrowsWhenParentCannotProvideARealLoadout()
    {
        var emptyParent = SolverTestScenario.Owned("Katress", PalGender.MALE);
        var entry = Entry(1);
        var root = Bred(
            new OwnedPalReference(
                emptyParent,
                [],
                new IV_Set(),
                new AttackProfile(Entry(0))
            ),
            Leaf(1),
            new AttackProfile(entry)
        );

        Assert.ThrowsException<InvalidOperationException>(() => Materialize(root, entry));
    }

    [TestMethod]
    public void Solve_RevalidatesAndMaterializesTheGenderAdjustedProfileEntry()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit &&
            !child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
        var noopAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit &&
            !child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
        var carrier = SolverTestScenario.Owned("Katress", PalGender.MALE);
        carrier.ActiveSkills = [requiredAttack];
        carrier.EquippedActiveSkills = [requiredAttack];
        var mate = SolverTestScenario.Owned("Wixen", PalGender.FEMALE);
        mate.ActiveSkills = [noopAttack];
        mate.EquippedActiveSkills = [noopAttack];

        var result = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                [carrier, mate],
                maxSpecialCakes: 0,
                maxBreedingSteps: 1,
                maxSolverIterations: 1
            ),
            child.Name,
            requiredGender: PalGender.MALE,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().Single();

        var entry = result.AttackProfile.Entries.Single();

        Assert.AreEqual(PalGender.MALE, result.Gender);
        Assert.AreEqual(0, entry.TotalSpecialCakes);
        Assert.IsTrue(result.AvgRequiredBreedings > 0);
        Assert.IsNotNull(result.MaterializedAttackInheritance);
    }

    private static BredPalReference Materialize(
        BredPalReference reference,
        AttackProfileEntry selectedEntry
    ) => (BredPalReference)new AttackResultMaterializer(Context(), Settings())
        .Materialize(reference, selectedEntry);

    private static BredPalReference Bred(
        IPalReference parent1,
        IPalReference parent2,
        AttackProfile attackProfile,
        Pal? child = null,
        PalGender gender = PalGender.WILDCARD,
        BreedingSolverSettings? settings = null
    )
    {
        settings ??= Settings();
        return new(
        settings.GameSettings,
        child ?? Child,
        parent1,
        parent2,
        [],
        passivesProbability: 1,
        new IV_Set(),
        ivsProbability: 1,
        attackProfile,
        materializedAttackInheritance: null,
        avgRequiredBreedings: null,
        gender
        );
    }

    private static OwnedPalReference Leaf(byte mask, bool hasNoop = false, int cakes = 0)
    {
        var instance = SolverTestScenario.Owned("Katress", PalGender.MALE);
        var learnedAttack = (mask & 1) != 0
            ? TargetAttack
            : SolverTestScenario.DB.ActiveSkills.First(attack =>
                attack != TargetAttack && attack.CanInherit != hasNoop
            );
        instance.ActiveSkills = [learnedAttack];
        instance.EquippedActiveSkills = [learnedAttack];

        return new(
            instance,
            [],
            new IV_Set(),
            new AttackProfile(hasNoop, Entry(mask, cakes))
        );
    }

    private static OwnedPalReference Leaf(ActiveSkill targetAttack, byte mask, int cakes = 0)
    {
        var instance = SolverTestScenario.Owned("Katress", PalGender.MALE);
        var learnedAttack = (mask & 1) != 0
            ? targetAttack
            : SolverTestScenario.DB.ActiveSkills.First(attack =>
                attack != targetAttack && attack.CanInherit
            );
        instance.ActiveSkills = [learnedAttack];
        instance.EquippedActiveSkills = [learnedAttack];

        return new(
            instance,
            [],
            new IV_Set(),
            new AttackProfile(Entry(mask, cakes))
        );
    }

    private static AttackProfileEntry Entry(byte mask, int cakes = 0) => new(mask, cakes);

    private static AttackTargetContext Context() =>
        new(
            new PalSpecifier { RequiredAttacks = [TargetAttack] },
            SolverTestScenario.DB
        );

    private static BreedingSolverSettings Settings() =>
        SolverTestScenario.Solver([], maxSpecialCakes: null).Settings;
}
