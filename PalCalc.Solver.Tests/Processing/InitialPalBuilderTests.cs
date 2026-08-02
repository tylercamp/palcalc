using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class InitialPalBuilderTests
{
    [TestMethod]
    public void Build_CombinesEquivalentOppositeGenderOwnedPals()
    {
        var male = SolverTestScenario.Owned(
            "Katress",
            PalGender.MALE
        );
        var female = SolverTestScenario.Owned(
            "Katress",
            PalGender.FEMALE
        );
        var configuredSolver = SolverTestScenario.Solver(
            [male, female],
            maxBreedingSteps: 1
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB
        ).Build(Target());

        Assert.AreEqual(1, seeds.Count);
        var composite =
            seeds.Single() as CompositeOwnedPalReference;
        Assert.IsNotNull(composite);
        Assert.AreSame(male, composite.Male.UnderlyingInstance);
        Assert.AreSame(female, composite.Female.UnderlyingInstance);
    }

    [TestMethod]
    public void Build_SelectsOwnedInstanceWithFewerIrrelevantPassives()
    {
        var irrelevant =
            "Swift".ToStandardPassive(SolverTestScenario.DB);
        var clean = SolverTestScenario.Owned(
            "Katress",
            PalGender.MALE
        );
        var noisy = SolverTestScenario.Owned(
            "Katress",
            PalGender.MALE,
            passives: [irrelevant]
        );
        var configuredSolver = SolverTestScenario.Solver(
            [noisy, clean],
            maxBreedingSteps: 1
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB
        ).Build(Target());

        Assert.AreEqual(1, seeds.Count);
        var selected = seeds.Single() as OwnedPalReference;
        Assert.IsNotNull(selected);
        Assert.AreSame(clean, selected.UnderlyingInstance);
    }

    [TestMethod]
    public void Build_AddsConfiguredWildPassiveCountVariants()
    {
        var katress = "Katress".ToPal(SolverTestScenario.DB);
        var configuredSolver = SolverTestScenario.Solver(
            ownedPals: [],
            maxBreedingSteps: 1,
            maxWildPals: 1,
            allowedWildPals: [katress]
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB
        ).Build(Target());

        CollectionAssert.AreEqual(
            new[] { 0, 1, 2, 3 },
            seeds
                .OfType<WildPalReference>()
                .Select(reference =>
                    reference.EffectivePassives.Count(
                        passive => passive is RandomPassiveSkill
                    )
                )
                .Order()
                .ToArray()
        );
    }

    [TestMethod]
    public void Build_OwnedLoadoutPrefersRequiredAttackWithoutMutatingSave()
    {
        var required = SolverTestScenario.DB.ActiveSkills.First(attack => !attack.CanInherit);
        var irrelevant = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit);
        var owned = SolverTestScenario.Owned("Katress", PalGender.MALE);
        owned.ActiveSkills = [irrelevant, required];
        owned.EquippedActiveSkills = [irrelevant];

        var selected = Build([owned], Target(required)).Single() as OwnedPalReference;

        Assert.IsNotNull(selected);
        Assert.AreSame(required, selected.ActualAttack);
        Assert.AreSame(required, selected.EffectiveAttack);
        CollectionAssert.AreEqual(new[] { irrelevant }, owned.EquippedActiveSkills);
    }

    [TestMethod]
    public void Build_OwnedLoadoutUsesFallbackOrder()
    {
        var inheritable = SolverTestScenario.DB.ActiveSkills
            .Where(attack => attack.CanInherit)
            .OrderBy(attack => attack.InternalName, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var nonInheritable = SolverTestScenario.DB.ActiveSkills.First(attack => !attack.CanInherit);
        var required = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit && !inheritable.Contains(attack));

        var neutral = SolverTestScenario.Owned("Katress", PalGender.MALE);
        neutral.ActiveSkills = [inheritable[0], nonInheritable];
        neutral.EquippedActiveSkills = [inheritable[0]];
        var equipped = SolverTestScenario.Owned("Wixen", PalGender.MALE);
        equipped.ActiveSkills = [inheritable[0], inheritable[1]];
        equipped.EquippedActiveSkills = [inheritable[1]];
        var stable = SolverTestScenario.Owned("Anubis", PalGender.MALE);
        stable.ActiveSkills = [inheritable[1], inheritable[0]];
        stable.EquippedActiveSkills = [];

        var seeds = Build([neutral, equipped, stable], Target(required)).OfType<OwnedPalReference>().ToList();

        Assert.AreSame(nonInheritable, seeds.Single(seed => seed.UnderlyingInstance == neutral).ActualAttack);
        Assert.IsNull(seeds.Single(seed => seed.UnderlyingInstance == neutral).EffectiveAttack);
        Assert.AreSame(inheritable[1], seeds.Single(seed => seed.UnderlyingInstance == equipped).ActualAttack);
        Assert.IsInstanceOfType<RandomActiveSkill>(seeds.Single(seed => seed.UnderlyingInstance == equipped).EffectiveAttack);
        Assert.AreSame(inheritable[0], seeds.Single(seed => seed.UnderlyingInstance == stable).ActualAttack);
    }

    [TestMethod]
    public void Build_WildLoadoutUsesOnlyLevelOneAttacks()
    {
        var wildPal = "Katress".ToPal(SolverTestScenario.DB);
        var level1 = wildPal.Level1ActiveSkills(SolverTestScenario.DB).First();
        var laterAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !wildPal.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var configuredSolver = SolverTestScenario.Solver(
            ownedPals: [],
            maxBreedingSteps: 1,
            maxWildPals: 1,
            allowedWildPals: [wildPal]
        );

        var wild = NewBuilder(configuredSolver).Build(Target(laterAttack)).OfType<WildPalReference>().First();

        Assert.AreSame(level1, wild.ActualAttack);
        Assert.IsInstanceOfType<RandomActiveSkill>(wild.EffectiveAttack);
    }

    [TestMethod]
    public void Build_CompositeResolvesEachGenderAttack()
    {
        var attacks = SolverTestScenario.DB.ActiveSkills.Where(attack => attack.CanInherit).Take(2).ToArray();
        var male = SolverTestScenario.Owned("Katress", PalGender.MALE);
        male.ActiveSkills = male.EquippedActiveSkills = [attacks[0]];
        var female = SolverTestScenario.Owned("Katress", PalGender.FEMALE);
        female.ActiveSkills = female.EquippedActiveSkills = [attacks[1]];
        var required = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit && !attacks.Contains(attack));

        var composite = Build([male, female], Target(required)).Single() as CompositeOwnedPalReference;

        Assert.IsNotNull(composite);
        Assert.AreSame(attacks[0], composite.WithGuaranteedGender(SolverTestScenario.DB, PalGender.MALE, false).ActualAttack);
        Assert.AreSame(attacks[1], composite.WithGuaranteedGender(SolverTestScenario.DB, PalGender.FEMALE, false).ActualAttack);
    }

    [TestMethod]
    public void Build_DoesNotReduceTargetAttackIntoIrrelevantAttack()
    {
        var required = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit);
        var irrelevant = SolverTestScenario.DB.ActiveSkills.First(attack => attack.CanInherit && attack != required);
        var targetBearer = SolverTestScenario.Owned("Katress", PalGender.MALE);
        targetBearer.ActiveSkills = targetBearer.EquippedActiveSkills = [required];
        var other = SolverTestScenario.Owned("Katress", PalGender.MALE);
        other.ActiveSkills = other.EquippedActiveSkills = [irrelevant];

        var seeds = Build([targetBearer, other], Target(required)).OfType<OwnedPalReference>().ToList();

        Assert.AreEqual(2, seeds.Count);
        Assert.IsTrue(seeds.Any(seed => seed.EffectiveAttack == required));
        Assert.IsTrue(seeds.Any(seed => seed.EffectiveAttack is RandomActiveSkill));
    }

    private static List<IPalReference> Build(IEnumerable<PalInstance> owned, PalSpecifier target)
    {
        var configuredSolver = SolverTestScenario.Solver(owned, maxBreedingSteps: 4);
        return NewBuilder(configuredSolver).Build(target);
    }

    private static InitialPalBuilder NewBuilder(SolverTestScenario.ConfiguredSolver configuredSolver) =>
        new(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB
        );

    private static PalSpecifier Target(ActiveSkill? requiredAttack = null) =>
        new()
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredAttack = requiredAttack,
        };
}
