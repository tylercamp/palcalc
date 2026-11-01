using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackResultMaterializerTests
{
    public TestContext TestContext { get; set; } = null!;
    private static readonly Pal Child = "Wixen Noct".ToPal(SolverTestScenario.DB);
    private static readonly ActiveSkill TargetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
        attack.CanInherit && !Child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack));

    private static readonly ActiveSkill SecondTargetAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
        attack.CanInherit && attack != TargetAttack && !Child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack));

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

    [TestMethod]
    public void Evaluate_MatchesTheSubsequentlyMaterializedResult()
    {
        var selectedEntry = Entry(mask: 1, cakes: 0);
        var root = Bred(Leaf(1), Leaf(0), new AttackProfile(selectedEntry));
        var materializer = new AttackResultMaterializer(Context(), Settings());

        var evaluation = materializer.Evaluate(root, selectedEntry.LearnedTargetMask).Single();
        var result = (BredPalReference)materializer.Materialize(materializer.Evaluate(root, selectedEntry.LearnedTargetMask).Single());

        Assert.AreEqual(result.BreedingEffort, evaluation.BreedingEffort);
        Assert.AreEqual(
            result.AttackProfile.Entries.Single().TotalSpecialCakes,
            evaluation.TotalSpecialCakes
        );
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Evaluate_UsesRealizedParentEffortDuringFinalization(bool parallelFarms)
    {
        var settings = Settings();
        settings.GameSettings.MultipleBreedingFarms = parallelFarms;
        var firstEntry = Entry(1);
        var secondEntry = Entry(0);
        var first = Bred(Leaf(1), Leaf(0), new AttackProfile(firstEntry), settings: settings,
            passivesProbability: 0.25f);
        var second = Bred(Leaf(0), Leaf(0), new AttackProfile(secondEntry), settings: settings,
            passivesProbability: 0.5f);
        var root = Bred(first, second, new AttackProfile(firstEntry), settings: settings);
        var materializer = new AttackResultMaterializer(Context(), settings);

        var evaluation = materializer.Evaluate(root, firstEntry.LearnedTargetMask).Single();
        var result = (BredPalReference)materializer.Materialize(materializer.Evaluate(root, firstEntry.LearnedTargetMask).Single());

        Assert.AreEqual(result.BreedingEffort, evaluation.BreedingEffort);
        Assert.IsTrue(result.BreedingEffort > root.BreedingEffort);
        Assert.AreEqual(0, evaluation.TotalSpecialCakes);
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
        var secondAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && attack != targetAttack && !child.Level1AttackInternalIds.Contains(attack.InternalName));
        var context = new AttackTargetContext(
            new PalSpecifier { RequiredAttacks = [targetAttack, secondAttack] },
            SolverTestScenario.DB
        );
        var settings = Settings();
        var selectedEntry = Entry(mask: 3, cakes: 1);
        var root = Bred(
            Leaf(targetAttack, 1),
            Leaf(secondAttack, 2),
            new AttackProfile(selectedEntry),
            child,
            PalGender.MALE,
            settings, method: BreedingMethod.SpecialCake
        );

        var result = (BredPalReference)MaterializeSingle(new AttackResultMaterializer(context, settings), root, selectedEntry);
        var expectedBreedings = BredPalReferenceEffort.WithGuaranteedGender(
            SolverTestScenario.DB,
            child,
            1,
            PalGender.MALE,
            useReverser: false
        );

        Assert.IsTrue(expectedBreedings > 1);
        Assert.AreEqual(expectedBreedings, result.AvgRequiredBreedings);
        Assert.AreEqual(expectedBreedings, result.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.AreEqual(expectedBreedings, result.MaterializedAttackInheritance.SpecialCakes);
    }

    [TestMethod]
    public void Materialize_CakeUsesTheFixedInheritancePassiveProbability()
    {
        var selectedEntry = Entry(mask: 3, cakes: 2);
        var root = Bred(
            Leaf(3),
            Leaf(0),
            new AttackProfile(selectedEntry),
            passivesProbability: 0.5f, method: BreedingMethod.SpecialCake
        );

        var result = Materialize(root, selectedEntry);

        Assert.AreEqual(AttackInheritanceMode.InheritAll, result.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(0.5f, result.PassivesProbability);
        Assert.AreEqual(2, result.AvgRequiredBreedings);
        Assert.AreEqual(2, result.MaterializedAttackInheritance.SpecialCakes);
    }

    [TestMethod]
    public void Materialize_CakeOnlyOutcomeAssignsCakesOnEveryRequiredEdge()
    {
        var intermediateEntry = Entry(3, cakes: 2);
        var intermediate = Bred(
            Leaf(3), Leaf(0), new AttackProfile(intermediateEntry),
            passivesProbability: 0.5f, method: BreedingMethod.SpecialCake
        );
        var rootEntry = Entry(3, cakes: 4);
        var root = Bred(
            intermediate, Leaf(0), new AttackProfile(rootEntry),
            passivesProbability: 0.5f, method: BreedingMethod.SpecialCake
        );

        Assert.AreEqual(2, root.AvgRequiredBreedings);
        var result = Materialize(root, rootEntry);
        var parent = new[] { result.Parent1, result.Parent2 }.OfType<BredPalReference>().Single();

        Assert.AreEqual(AttackInheritanceMode.InheritAll, parent.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(AttackInheritanceMode.InheritAll, result.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(2, parent.MaterializedAttackInheritance.SpecialCakes);
        Assert.AreEqual(2, result.MaterializedAttackInheritance.SpecialCakes);
        Assert.AreEqual(4, result.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.AreEqual(0.5f, result.PassivesProbability);
    }

    [TestMethod]
    public void Materialize_CakeOnlyOutcomeCannotFallBackToNormalInheritance()
    {
        var ordinaryEntry = Entry(1, cakes: 0);
        var root = Bred(
            Leaf(1), Leaf(0), new AttackProfile(ordinaryEntry),
            method: BreedingMethod.SpecialCake
        );

        Assert.ThrowsException<InvalidOperationException>(() => Materialize(root, ordinaryEntry));
    }

    [TestMethod]
    public void Materialize_NormalChoiceUsesOnlyOrdinaryPassiveProbability()
    {
        var ordinaryEntry = Entry(1, cakes: 0);
        var root = Bred(
            Leaf(1), Leaf(0), new AttackProfile(ordinaryEntry),
            passivesProbability: 0.25f
        );

        Assert.AreEqual(4, root.AvgRequiredBreedings);
        var result = Materialize(root, ordinaryEntry);

        Assert.AreEqual(AttackInheritanceMode.Normal, result.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(0.25f, result.PassivesProbability);
        Assert.AreEqual(8, result.AvgRequiredBreedings);
        Assert.AreEqual(0, result.AttackProfile.Entries.Single().TotalSpecialCakes);
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
                result.Parent2
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
        var intermediateEntry = Entry(mask: 3, cakes: 2);
        var intermediate = Bred(
            Leaf(3, cakes: 1),
            Leaf(0),
            new AttackProfile(intermediateEntry), method: BreedingMethod.SpecialCake
        );
        var rootEntry = Entry(mask: 3, cakes: 3);
        var root = Bred(
            intermediate,
            Leaf(0),
            new AttackProfile(rootEntry), method: BreedingMethod.SpecialCake
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
    public void Materialize_RejectsCakeForSingleNonInnateAttack()
    {
        var entry = Entry(1, cakes: 1);
        var root = Bred(Leaf(1), Leaf(0), new AttackProfile(entry), method: BreedingMethod.SpecialCake);
        Assert.ThrowsException<InvalidOperationException>(() => Materialize(root, entry));
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

        Assert.AreSame(first, materializer.Materialize(materializer.Evaluate(first, entry.LearnedTargetMask).Single()));
        Assert.AreSame(second, materializer.Materialize(materializer.Evaluate(second, entry.LearnedTargetMask).Single()));
    }

    [TestMethod]
    public void Materialize_UsesAnyAttackWhenParentHasNoRelevantLearnedAttack()
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

        var result = Materialize(root, entry);

        var filler = result.MaterializedAttackInheritance.Parent1Loadout
            .Concat(result.MaterializedAttackInheritance.Parent2Loadout)
            .Single(attack => attack is RandomActiveSkill);
        Assert.IsInstanceOfType<RandomActiveSkill>(filler);
        Assert.AreEqual("Any Attack", filler.Name);
    }

    [TestMethod]
    public void Materialize_UsesAnyAttackForBothParentsWhenChildAttackIsInnate()
    {
        var innateAttack = Child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var context = new AttackTargetContext(
            new PalSpecifier { RequiredAttacks = [innateAttack] },
            SolverTestScenario.DB
        );
        var emptyParent1 = SolverTestScenario.Owned("Katress", PalGender.MALE);
        var emptyParent2 = SolverTestScenario.Owned("Wixen", PalGender.FEMALE);
        var entry = Entry(1);
        var root = Bred(
            new OwnedPalReference(emptyParent1, [], new IV_Set(), new AttackProfile(Entry(0))),
            new OwnedPalReference(emptyParent2, [], new IV_Set(), new AttackProfile(Entry(0))),
            new AttackProfile(entry)
        );

        var result = (BredPalReference)MaterializeSingle(new AttackResultMaterializer(context, Settings()), root, entry);

        Assert.IsInstanceOfType<RandomActiveSkill>(
            result.MaterializedAttackInheritance.Parent1Loadout.Single()
        );
        Assert.IsInstanceOfType<RandomActiveSkill>(
            result.MaterializedAttackInheritance.Parent2Loadout.Single()
        );
    }

    [TestMethod]
    public void Materialize_PreservesNoopAttackWhenNormalInheritanceDependsOnIt()
    {
        var entry = Entry(1);
        var result = Materialize(
            Bred(Leaf(0, hasNoop: true), Leaf(1), new AttackProfile(entry)),
            entry
        );

        var filler = new[]
            {
                result.MaterializedAttackInheritance.Parent1Loadout,
                result.MaterializedAttackInheritance.Parent2Loadout,
            }
            .Single(loadout => !loadout.Contains(TargetAttack))
            .Single();
        Assert.IsNotInstanceOfType<RandomActiveSkill>(filler);
        Assert.IsFalse(filler.CanInherit);
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
    ) => (BredPalReference)MaterializeSingle(new AttackResultMaterializer(Context(), Settings()), reference, selectedEntry);

    private static BredPalReference Bred(
        IPalReference parent1,
        IPalReference parent2,
        AttackProfile attackProfile,
        Pal? child = null,
        PalGender gender = PalGender.WILDCARD,
        BreedingSolverSettings? settings = null,
        float passivesProbability = 1,
        BreedingMethod method = BreedingMethod.Ordinary
    )
    {
        settings ??= Settings();
        return new(
            settings.GameSettings,
            child ?? Child,
            gender,
            parent1,
            parent2,
            avgRequiredBreedings: null,
            [],
            new BreedingOutcome(method, passivesProbability),
            new IV_Set(),
            ivsProbability: 1,
            attackProfile,
            materializedAttackInheritance: null
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
        instance.ActiveSkills = (mask & 2) != 0 ? [learnedAttack, SecondTargetAttack] : [learnedAttack];
        instance.EquippedActiveSkills = instance.ActiveSkills;

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
        instance.ActiveSkills = (mask & 2) != 0 ? [learnedAttack, SecondTargetAttack] : [learnedAttack];
        instance.EquippedActiveSkills = instance.ActiveSkills;

        return new(
            instance,
            [],
            new IV_Set(),
            new AttackProfile(Entry(mask, cakes))
        );
    }

    private static AttackProfileEntry Entry(byte mask, int cakes = 0) => new(mask, cakes);

    [DataTestMethod]
    [DataRow(3)]
    [DataRow(6)]
    public void SharedSubtreesAreEvaluatedOnceAndConstructedOnlyAfterSelection(int attackCount)
    {
        var settings = Settings();
        var attacks = SolverTestScenario.DB.ActiveSkills.Where(attack => attack.CanInherit &&
            !Child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)).Take(attackCount).ToList();
        var context = new AttackTargetContext(new PalSpecifier { RequiredAttacks = attacks }, settings.DB);
        var mask = (byte)((1 << attackCount) - 1);
        var instance = SolverTestScenario.Owned("Katress", PalGender.MALE);
        instance.ActiveSkills = attacks;
        instance.EquippedActiveSkills = attacks.Take(3).ToList();
        IPalReference root = new OwnedPalReference(instance, [], new IV_Set(),
            new AttackProfile(Enumerable.Range(0, mask + 1).Select(i => Entry((byte)i)).ToArray()));
        var cakes = 0;
        for (var depth = 0; depth < 6; depth++)
        {
            cakes = 2 * cakes + 1;
            root = Bred(root, root, new AttackProfile(Entry(mask, cakes)),
                settings: settings, method: BreedingMethod.SpecialCake);
        }
        var materializer = new AttackResultMaterializer(context, settings);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var allocated = GC.GetAllocatedBytesForCurrentThread();

        var evaluated = materializer.Evaluate(root, mask);
        var evaluationMs = watch.Elapsed.TotalMilliseconds;
        var evaluationBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.AreEqual(0, materializer.MaterializedCount);
        Assert.AreEqual(1, evaluated.Count);
        Assert.IsTrue(materializer.EvaluatedStateCount <= mask + 7);
        Assert.IsTrue(materializer.EvaluationCacheHits > 0);
        watch.Restart();
        allocated = GC.GetAllocatedBytesForCurrentThread();
        var realized = materializer.Materialize(evaluated.Single());
        TestContext.WriteLine(
            $"{attackCount} attacks: evaluation {evaluationMs:F3} ms / {evaluationBytes} bytes; " +
            $"materialization {watch.Elapsed.TotalMilliseconds:F3} ms / " +
            $"{GC.GetAllocatedBytesForCurrentThread() - allocated} bytes; " +
            $"{materializer.EvaluatedStateCount} states, {materializer.EvaluationCacheHits} cache hits, " +
            $"{materializer.MaterializedCount} materialized nodes.");
        Assert.AreEqual(cakes, realized.AttackProfile.Entries.Single().TotalSpecialCakes);
        Assert.AreEqual(evaluated.Single().BreedingEffort, realized.BreedingEffort);
    }

    [TestMethod]
    public void Evaluate_PreservesTradeoffsAndMaterializesTheirExactParentSelections()
    {
        var carrier = Leaf(1);
        var parent = new OwnedPalReference(carrier.UnderlyingInstance, [], carrier.IVs,
            new AttackProfile(Entry(0), Entry(1, cakes: 1)));
        var root = Bred(parent, Leaf(1), new AttackProfile(Entry(1)));
        var materializer = new AttackResultMaterializer(Context(), Settings());

        var evaluations = materializer.Evaluate(root, 1);

        Assert.AreEqual(2, evaluations.Count);
        Assert.AreEqual(0, materializer.MaterializedCount);
        Assert.IsTrue(evaluations.Single(e => e.TotalSpecialCakes == 0).BreedingEffort >
            evaluations.Single(e => e.TotalSpecialCakes == 1).BreedingEffort);
        foreach (var evaluation in evaluations)
        {
            var realized = (BredPalReference)materializer.Materialize(evaluation);
            Assert.AreEqual(evaluation.BreedingEffort, realized.BreedingEffort);
            Assert.AreEqual(evaluation.TotalSpecialCakes, realized.AttackProfile.Entries.Single().TotalSpecialCakes);
            Assert.AreEqual(evaluation.Choice!.Value.Parent1Entry.TotalSpecialCakes,
                materializer.Materialize(evaluation.Parent1).AttackProfile.Entries
                    .Single(entry => entry.LearnedTargetMask == evaluation.Parent1.TargetMask).TotalSpecialCakes);
        }
        Assert.AreSame(evaluations, materializer.Evaluate(root, 1));
        Assert.IsTrue(materializer.EvaluationCacheHits > 0);
    }

    [TestMethod]
    public void Evaluate_CanUseAHigherEstimatedCakeWitnessForTheSameMask()
    {
        var carrier = Leaf(1);
        var parent = new OwnedPalReference(carrier.UnderlyingInstance, [], carrier.IVs,
            new AttackProfile(Entry(0), Entry(1, cakes: 1)));
        var root = Bred(parent, Leaf(1), new AttackProfile(Entry(1)));
        var materializer = new AttackResultMaterializer(Context(), Settings());
        var fastest = materializer.Evaluate(root, 1).MinBy(result => result.BreedingEffort)!;

        Assert.AreEqual(1, fastest.TotalSpecialCakes);
        Assert.AreEqual(1, ((BredPalReference)materializer.Materialize(fastest)).AvgRequiredBreedings);
    }

    [TestMethod]
    public void EqualMetricWitnessesUseDeterministicProfileTieBreaks()
    {
        var innate = Child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var context = new AttackTargetContext(new PalSpecifier { RequiredAttacks = [innate] },
            SolverTestScenario.DB);
        var leaf = Leaf(0);
        var parent = new OwnedPalReference(leaf.UnderlyingInstance, [], leaf.IVs,
            new AttackProfile(Entry(1), Entry(0)));
        var root = Bred(parent, parent, new AttackProfile(Entry(1)));
        var materializer = new AttackResultMaterializer(context, Settings());

        var evaluation = materializer.Evaluate(root, 1).Single();

        Assert.AreEqual(AttackInheritanceMode.Baseline, evaluation.Choice!.Value.Mode);
        Assert.AreEqual((byte)0, evaluation.Parent1.TargetMask);
        Assert.AreEqual((byte)0, evaluation.Parent2.TargetMask);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EffortBoundsRemainLowerThroughGenderSurgeryAndTraining(bool parallelFarms)
    {
        var settings = Settings();
        settings.GameSettings.MultipleBreedingFarms = parallelFarms;
        var leaf = Leaf(1);
        var trained = new OwnedPalReference(leaf.UnderlyingInstance, [], leaf.IVs,
            leaf.AttackProfile, new LevelRequirements(1, 50));
        var parent = Bred(trained, Leaf(0), new AttackProfile(Entry(1)),
            gender: PalGender.FEMALE, settings: settings, passivesProbability: .25f);
        var other = Bred(Leaf(0), Leaf(0), new AttackProfile(Entry(0)), settings: settings);
        var root = Bred(parent, other, new AttackProfile(Entry(1)), settings: settings);
        var wrapped = new SurgeryTablePalReference(root, []);
        var materializer = new AttackResultMaterializer(Context(), settings);
        var bounds = materializer.LowerBounds(wrapped, Entry(1));

        foreach (var result in materializer.Evaluate(wrapped, 1))
        {
            Assert.IsTrue(bounds.Covers(result.Metrics));
            Assert.AreEqual(result.BreedingEffort, materializer.Materialize(result).BreedingEffort);
        }
    }

    private static AttackTargetContext Context() =>
        new(
            new PalSpecifier { RequiredAttacks = [TargetAttack, SecondTargetAttack] },
            SolverTestScenario.DB
        );

    private static BreedingSolverSettings Settings() =>
        SolverTestScenario.Solver([], maxSpecialCakes: null).Settings;
    private static IPalReference MaterializeSingle(
        AttackResultMaterializer materializer, IPalReference reference, AttackProfileEntry entry) =>
        materializer.Materialize(materializer.Evaluate(reference, entry.LearnedTargetMask).Single());

}
