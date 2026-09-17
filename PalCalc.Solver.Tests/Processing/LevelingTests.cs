using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class LevelingTests
{
    private static readonly PalDB DB = SolverTestScenario.DB;

    // A separate species object keeps the test schedule independent of the embedded DB.
    private static Pal Species(int? minimum = 5)
    {
        var original = "Katress".ToPal(DB);
        var attacks = DB.ActiveSkills.Where(a => a.CanInherit).Take(3).ToArray();
        return new Pal
        {
            Id = original.Id, Name = original.Name, InternalName = original.InternalName,
            MinWildLevel = minimum,
            InternalAttackLeveling = attacks.Select((a, i) => new PalInternalAttackLevel
            {
                AttackInternalId = a.InternalName, Level = new[] { 1, 10, 20 }[i]
            }).ToList()
        };
    }

    private static PalInstance Owned(Pal species, int level, PalGender gender = PalGender.MALE)
    {
        var instance = SolverTestScenario.Owned(species.Name, gender);
        instance.Pal = species;
        instance.Level = level;
        instance.ActiveSkills = species.Level1ActiveSkills(DB).ToList();
        return instance;
    }

    private static PalSpecifier Target(Pal species) => new()
    {
        Pal = "Wixen Noct".ToPal(DB),
        RequiredAttacks = species.AttackLeveling(DB).Select(e => e.Attack).ToList()
    };

    private static List<IPalReference> Build(Pal species, params PalInstance[] owned)
    {
        var settings = SolverTestScenario.Solver(owned, maxBreedingSteps: int.MaxValue,
            maxWildPals: 1, allowedWildPals: [species]).Settings;
        var target = Target(species);
        return new InitialPalBuilder(settings, DB.BreedingMechanics, settings.BreedingDB,
            new AttackTargetContext(target, DB)).Build(target);
    }

    [TestMethod]
    public void OwnedVariantsAreCumulativeDistinctAndPreserveGenderCopies()
    {
        var species = Species();
        var instance = Owned(species, 5);
        var seeds = Build(species, instance).OfType<OwnedPalReference>().ToArray();
        CollectionAssert.AreEqual(new byte[] { 1, 3, 7 },
            seeds.Select(p => p.AttackProfile.Entries.Single().LearnedTargetMask).ToArray());
        Assert.AreEqual(3, seeds.Distinct().Count());
        Assert.IsNull(seeds[0].LevelRequirements);
        Assert.AreEqual(new LevelRequirements(5, 10), seeds[1].LevelRequirements);
        Assert.AreEqual(new LevelRequirements(5, 20), seeds[2].LevelRequirements);
        Assert.AreSame(instance, seeds[2].UnderlyingInstance);
        Assert.AreEqual(5, instance.Level);
        Assert.AreEqual(1, instance.ActiveSkills.Count);
        Assert.AreEqual(seeds[2].LevelRequirements,
            seeds[2].WithGuaranteedGender(DB, PalGender.FEMALE, true).LevelRequirements);
    }

    [TestMethod]
    public void TrainingSurvivesOwnedReductionWithoutCreatingTrainedComposites()
    {
        var species = Species();
        var low = Owned(species, 5);
        var high = Owned(species, 9);
        var female = Owned(species, 8, PalGender.FEMALE);
        var seeds = Build(species, low, high, female);
        Assert.AreEqual(1, seeds.OfType<CompositeOwnedPalReference>().Count());
        var trained = seeds.OfType<OwnedPalReference>().ToArray();
        Assert.AreEqual(4, trained.Length);
        Assert.IsTrue(trained.All(p => p.LevelRequirements is not null));
        Assert.IsTrue(trained.Where(p => p.Gender == PalGender.MALE)
            .All(p => ReferenceEquals(high, p.UnderlyingInstance)));
    }

    [TestMethod]
    public void WildVariantsUseMinimumLevelOrOneAndPreserveGenderCopies()
    {
        foreach (var minimum in new int?[] { 5, null })
        {
            var seeds = Build(Species(minimum)).OfType<WildPalReference>()
                .Where(p => p.EffectivePassives.Count == 0).ToArray();
            Assert.AreEqual(3, seeds.Length);
            Assert.IsNull(seeds[0].LevelRequirements);
            Assert.AreEqual(new LevelRequirements(minimum ?? 1, 20), seeds[2].LevelRequirements);
            Assert.AreEqual(3, seeds.Distinct().Count());
            Assert.AreEqual(seeds[2].LevelRequirements,
                seeds[2].WithGuaranteedGender(DB, PalGender.FEMALE, false).LevelRequirements);
        }
    }

    [TestMethod]
    public void KnownAttacksSameLevelAndInactiveTargetsDoNotCreateRedundantTraining()
    {
        var species = Species();
        species.InternalAttackLeveling[2].Level = 10;
        var instance = Owned(species, 5);
        Assert.AreEqual(2, Build(species, instance).Count);
        instance.ActiveSkills = species.AttackLeveling(DB).Select(e => e.Attack).ToList();
        Assert.AreEqual(1, Build(species, instance).Count);
        var settings = SolverTestScenario.Solver([Owned(species, 5)]).Settings;
        var target = Target(species);
        target.RequiredAttacks = [];
        var seeds = new InitialPalBuilder(settings, DB.BreedingMechanics, settings.BreedingDB,
            new AttackTargetContext(target, DB)).Build(target);
        Assert.IsTrue(seeds.All(p => p.LevelRequirements is null));
    }

    private static OwnedPalReference Trained(int initial, int final) =>
        new(Owned(Species(), initial), [], new(), new AttackProfile(new AttackProfileEntry(7, 0)),
            new LevelRequirements(initial, final));

    [TestMethod]
    public void DisablingTrainingKeepsOnlyBaselineOwnedAndWildAttacks()
    {
        var species = Species(10);
        var target = Target(species);
        var context = new AttackTargetContext(target, DB);
        foreach (var wild in new[] { false, true })
        {
            var settings = SolverTestScenario.Solver(wild ? [] : [Owned(species, 5)],
                maxBreedingSteps: int.MaxValue, maxWildPals: 1, allowedWildPals: [species], trainPals: false).Settings;
            var seeds = new InitialPalBuilder(settings, DB.BreedingMechanics, settings.BreedingDB, context).Build(target);
            Assert.IsTrue(seeds.All(p => p.LevelRequirements is null));
            Assert.IsTrue(seeds.All(p => p.AttackProfile.Entries.Single().LearnedTargetMask == (wild ? 3 : 1)));
            Assert.IsTrue(seeds.Count > 0);
        }
        var instance = Owned(species, 5);
        var results = SolverTestScenario.Solve(SolverTestScenario.Solver([instance], maxBreedingSteps: 0,
            trainPals: false), species.Name, species.AttackLeveling(DB).Select(e => e.Attack));
        Assert.AreEqual(0, results.Count);
    }

    private static BredPalReference Bred(IPalReference first, IPalReference second) =>
        new(new GameSettings(), "Wixen Noct".ToPal(DB), PalGender.WILDCARD, first, second,
            null, [], 1, 1, new(), 1, AttackProfile.Inactive, null);

    private static BredPalReference ReusedAcrossBranches(IPalReference first, IPalReference second)
    {
        var mate1 = new OwnedPalReference(Owned(Species(), 1, PalGender.FEMALE), [], new(), AttackProfile.Inactive);
        var mate2 = new OwnedPalReference(Owned(Species(), 1, PalGender.FEMALE), [], new(), AttackProfile.Inactive);
        return Bred(Bred(first, mate1), Bred(second, mate2));
    }

    [TestMethod]
    public void PruningCountsReuseOnceAndPrefersCountBeforeLevels()
    {
        var reused = Trained(1, 20);
        var one = ReusedAcrossBranches(reused, reused);
        var two = ReusedAcrossBranches(Trained(9, 10), Trained(9, 10));
        Assert.AreEqual((1, 19), MinimumLevelingPruning.TrainingOf(one));
        Assert.AreEqual(38, one.TotalRequiredLevels);
        Assert.AreEqual(19, ((IPalReference)reused).TotalRequiredLevels);
        Assert.AreEqual(38, new SurgeryTablePalReference(one, []).TotalRequiredLevels);
        var rule = new MinimumLevelingPruning(CancellationToken.None);
        IPalReference[] results = [two, one];
        Assert.AreSame(one, rule.Apply(results, new CachedResultData(results)).Single());
        // One distinct training reference with fewer levels wins the secondary comparison.
        var shortPal = Trained(10, 20);
        var shorter = ReusedAcrossBranches(shortPal, shortPal);
        results = [one, shorter];
        Assert.AreSame(shorter, rule.Apply(results, new CachedResultData(results)).Single());
        Assert.IsNull(((IPalReference)one).LevelRequirements);
        results = [one, shorter];
        Assert.AreSame(shorter, ResultPruningPolicy.Default.Create(CancellationToken.None)
            .Apply(results, new CachedResultData(results)).Single());
    }

    [TestMethod]
    public void SolveCanTrainOwnedTargetWithoutBreeding()
    {
        var species = Species();
        var instance = Owned(species, 5);
        var solver = SolverTestScenario.Solver([instance], maxBreedingSteps: 0);
        var results = SolverTestScenario.Solve(solver, species.Name,
            species.AttackLeveling(DB).Select(e => e.Attack));
        var result = (OwnedPalReference)results.Single();
        Assert.AreEqual(new LevelRequirements(5, 20), result.LevelRequirements);
        Assert.AreEqual(0, result.NumTotalBreedingSteps);
    }

    [TestMethod]
    public void MaterializationReconstructsNonInheritableAttacksGainedDuringTraining()
    {
        var child = "Wixen Noct".ToPal(DB);
        var attacks = DB.ActiveSkills.Where(a => a.CanInherit && !child.Level1ActiveSkills(DB).Contains(a))
            .Take(2).ToArray();
        var noop = DB.ActiveSkills.First(a => !a.CanInherit);
        var species = Species();
        species.InternalAttackLeveling =
        [
            new() { AttackInternalId = noop.InternalName, Level = 15 },
            new() { AttackInternalId = attacks[1].InternalName, Level = 20 }
        ];
        var target = new PalSpecifier { Pal = child, RequiredAttacks = attacks.ToList() };
        foreach (var useWild in new[] { false, true })
        {
            var instance = Owned(species, 5);
            var settings = SolverTestScenario.Solver(useWild ? [] : [instance],
                maxBreedingSteps: int.MaxValue, maxWildPals: 1, allowedWildPals: [species]).Settings;
            var context = new AttackTargetContext(target, DB);
            var trained = new InitialPalBuilder(settings, DB.BreedingMechanics, settings.BreedingDB, context)
                .Build(target).First(p => p.LevelRequirements?.FinalLevel == 20);
            Assert.IsTrue(trained.AttackProfile.HasNoopAttack);
            var carrierInstance = SolverTestScenario.Owned("Katress", PalGender.MALE);
            carrierInstance.ActiveSkills = [attacks[0]];
            var carrier = new OwnedPalReference(carrierInstance, [], new(),
                new AttackProfile(new AttackProfileEntry(1, 0)));
            var entry = new AttackProfileEntry(1, 0);
            var bred = new BredPalReference(settings.GameSettings, child, PalGender.WILDCARD,
                carrier, trained, null, [], 1, 1, new(), 1, new AttackProfile(entry), null);
            var result = (BredPalReference)new AttackResultMaterializer(context, settings).Materialize(bred, entry);
            var instructions = result.MaterializedAttackInheritance;
            Assert.IsNotNull(instructions);
            Assert.AreEqual(1f, instructions.AttackProbability);
            Assert.IsTrue(instructions.Parent1Loadout.Contains(noop) || instructions.Parent2Loadout.Contains(noop));
            Assert.AreEqual((1, 15), MinimumLevelingPruning.TrainingOf(result));
        }
    }
}
