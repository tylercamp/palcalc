using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Attacks;

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
            maxSpecialCakes: 0,
            maxBreedingSteps: 1
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB,
            new AttackTargetContext(Target(), configuredSolver.Settings.DB)
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
            maxSpecialCakes: 0,
            maxBreedingSteps: 1
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB,
            new AttackTargetContext(Target(), configuredSolver.Settings.DB)
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
            maxSpecialCakes: 0,
            maxBreedingSteps: 1,
            maxWildPals: 1,
            allowedWildPals: [katress]
        );

        var seeds = new InitialPalBuilder(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB,
            new AttackTargetContext(Target(), configuredSolver.Settings.DB)
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
    public void Build_WildLoadoutUsesOnlyLevelOneAttacks()
    {
        var wildPal = "Katress".ToPal(SolverTestScenario.DB);
        var level1 = wildPal.Level1ActiveSkills(SolverTestScenario.DB).First();
        var laterAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !wildPal.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var configuredSolver = SolverTestScenario.Solver(
            ownedPals: [],
            maxSpecialCakes: 0,
            maxBreedingSteps: 1,
            maxWildPals: 1,
            allowedWildPals: [wildPal]
        );

        var target = Target(laterAttack);
        var wild = NewBuilder(configuredSolver, target).Build(target).OfType<WildPalReference>().First();

        Assert.AreEqual((byte)0, wild.AttackProfile.Entries.Single().LearnedTargetMask);
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
        CollectionAssert.AreEquivalent(
            new byte[] { 0, 1 },
            seeds.Select(seed => seed.AttackProfile.Entries.Single().LearnedTargetMask).ToArray()
        );
    }

    [TestMethod]
    public void Build_OwnedProfileUsesAllLearnedAttacksAndNeutralCapability()
    {
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var inheritable = InheritableAttackNotInnateTo(pal);
        var nonInheritable = NonInheritableAttackNotInnateTo(pal);
        var owned = SolverTestScenario.Owned(pal.Name, PalGender.MALE);
        owned.ActiveSkills = [inheritable, nonInheritable];
        owned.EquippedActiveSkills = [nonInheritable];
        var target = Target(inheritable, nonInheritable);

        var seed = Build([owned], target).OfType<OwnedPalReference>().Single();
        var context = new AttackTargetContext(target, SolverTestScenario.DB);

        Assert.AreEqual(context.FullTargetMask, seed.AttackProfile.Entries.Single().LearnedTargetMask);
        Assert.IsTrue(seed.AttackProfile.HasNoopAttack);
        Assert.AreEqual(0, context.MaskOf([nonInheritable]) & context.InheritableTargetMask);
    }

    [TestMethod]
    public void Build_WildProfileUsesOnlyLevelOneAttacks()
    {
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var level1 = pal.Level1ActiveSkills(SolverTestScenario.DB).First();
        var later = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack != level1 && !pal.Level1AttackInternalIds.Contains(attack.InternalName)
        );
        var target = Target(level1, later);
        var configuredSolver = SolverTestScenario.Solver(
            ownedPals: [], maxSpecialCakes: 0, maxBreedingSteps: 1, maxWildPals: 1, allowedWildPals: [pal]
        );

        var wild = NewBuilder(configuredSolver, target).Build(target).OfType<WildPalReference>().First();
        var context = new AttackTargetContext(target, SolverTestScenario.DB);

        Assert.AreEqual(
            context.MaskOf(pal.Level1ActiveSkills(SolverTestScenario.DB)),
            wild.AttackProfile.Entries.Single().LearnedTargetMask
        );
    }

    [TestMethod]
    public void Build_DifferentGenderProfilesDoNotFormComposite()
    {
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var required = InheritableAttackNotInnateTo(pal);
        var male = SolverTestScenario.Owned(pal.Name, PalGender.MALE);
        var female = SolverTestScenario.Owned(pal.Name, PalGender.FEMALE);
        male.ActiveSkills = [required];
        female.ActiveSkills = [];
        var target = Target(required);

        var seeds = Build([male, female], target);

        Assert.AreEqual(2, seeds.Count);
        Assert.IsFalse(seeds.Any(seed => seed is CompositeOwnedPalReference));
        Assert.AreEqual(2, seeds.Select(seed => seed.AttackProfile.Entries.Single().LearnedTargetMask).Distinct().Count());
    }

    [TestMethod]
    public void Build_EquivalentGenderProfilesFormComposite()
    {
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var required = InheritableAttackNotInnateTo(pal);
        var male = SolverTestScenario.Owned(pal.Name, PalGender.MALE);
        var female = SolverTestScenario.Owned(pal.Name, PalGender.FEMALE);
        male.ActiveSkills = female.ActiveSkills = [required];
        var target = Target(required);

        var composite = Build([male, female], target).Single() as CompositeOwnedPalReference;

        Assert.IsNotNull(composite);
        Assert.AreEqual((byte)1, composite.AttackProfile.Entries.Single().LearnedTargetMask);
    }

    [TestMethod]
    public void WithGuaranteedGender_PreservesAttackProfileCapabilities()
    {
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var profile = new AttackProfile(true, new AttackProfileEntry(1, 0, TimeSpan.Zero, 0, false));
        var owned = new OwnedPalReference(
            SolverTestScenario.Owned(pal.Name, PalGender.MALE), [], new(),
            attackProfile: profile
        );
        var wild = new WildPalReference(
            pal, [], 0, SolverTestScenario.DB.BreedingMechanics,
            attackProfile: profile
        );
        var bred = new BredPalReference(
            new GameSettings(), pal, owned, owned, [], 1, new(), 1,
            attackProfile: profile,
            materializedAttackInheritance: null,
            avgRequiredBreedings: null,
            gender: PalGender.WILDCARD
        );

        foreach (var reference in new IPalReference[]
        {
            owned.WithGuaranteedGender(SolverTestScenario.DB, PalGender.FEMALE, true),
            wild.WithGuaranteedGender(SolverTestScenario.DB, PalGender.FEMALE, false),
            bred.WithGuaranteedGender(SolverTestScenario.DB, PalGender.FEMALE, true),
        })
        {
            Assert.AreEqual((byte)1, reference.AttackProfile.Entries.Single().LearnedTargetMask);
            Assert.IsTrue(reference.AttackProfile.HasNoopAttack);
        }
    }

    private static List<IPalReference> Build(IEnumerable<PalInstance> owned, PalSpecifier target)
    {
        var configuredSolver = SolverTestScenario.Solver(owned, maxSpecialCakes: 0, maxBreedingSteps: 4);
        return NewBuilder(configuredSolver, target).Build(target);
    }

    private static InitialPalBuilder NewBuilder(SolverTestScenario.ConfiguredSolver configuredSolver, PalSpecifier target) =>
        new(
            configuredSolver.Settings,
            configuredSolver.Settings.DB.BreedingMechanics,
            configuredSolver.Settings.BreedingDB,
            new AttackTargetContext(target, configuredSolver.Settings.DB)
        );

    private static PalSpecifier Target(params ActiveSkill[] requiredAttacks) =>
        new()
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredAttacks = requiredAttacks.ToList(),
        };

    private static ActiveSkill InheritableAttackNotInnateTo(Pal pal) =>
        SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !pal.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );

    private static ActiveSkill NonInheritableAttackNotInnateTo(Pal pal) =>
        SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit && !pal.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
}
