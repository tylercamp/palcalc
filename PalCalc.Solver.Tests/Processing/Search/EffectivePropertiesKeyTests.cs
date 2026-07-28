using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Tests.Processing.Search;

[TestClass]
public class EffectivePropertiesKeyTests
{
    [TestMethod]
    public void PassiveSetKey_IsOrderIndependentAndPreservesDuplicates()
    {
        // note: "preserves duplicates" check just sets expectations on behavior,
        //       it's not actually a desired property.
        //
        // TODO - debug-check to ensure passives aren't duplicated

        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var runner = "Runner".ToStandardPassive(SolverTestScenario.DB);

        var forward = new PassiveSetKey([swift, runner, runner]);
        var reversed = new PassiveSetKey([runner, swift, runner]);
        var withoutDuplicate = new PassiveSetKey([swift, runner]);

        Assert.AreEqual(forward, reversed);
        Assert.AreEqual(forward.GetHashCode(), reversed.GetHashCode());
        Assert.AreNotEqual(forward, withoutDuplicate);
    }

    [TestMethod]
    public void KeyProvider_GroupsEquivalentDefaultStates()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var runner = "Runner".ToStandardPassive(SolverTestScenario.DB);
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var first = new TestPalReference(
            pal,
            PalGender.MALE,
            [swift, runner],
            new IV_Set(
                HP: new IV_Value(true, 90, 100),
                Attack: new IV_Value(false, 0, 40),
                Defense: new IV_Value(false, 0, 20)
            )
        );
        var equivalent = new TestPalReference(
            pal,
            PalGender.MALE,
            [runner, swift],
            new IV_Set(
                HP: new IV_Value(true, 50, 60),
                Attack: new IV_Value(false, 10, 80),
                Defense: new IV_Value(false, 20, 90)
            )
        );
        var provider = DefaultEffectivePropertiesKeyProvider.Instance;

        Assert.AreEqual(provider.KeyOf(first), provider.KeyOf(equivalent));
    }

    [TestMethod]
    public void FrontierIndex_DoesNotMergeCollidingPassiveHashes()
    {
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var first = new TestPalReference(
            pal,
            PalGender.MALE,
            ["Swift".ToStandardPassive(SolverTestScenario.DB)],
            new IV_Set(),
            effectivePassivesHash: 12345
        );
        var second = new TestPalReference(
            pal,
            PalGender.MALE,
            ["Runner".ToStandardPassive(SolverTestScenario.DB)],
            new IV_Set(),
            effectivePassivesHash: 12345
        );
        var provider = DefaultEffectivePropertiesKeyProvider.Instance;
        var index = new FrontierIndex(provider);

        Assert.AreNotEqual(provider.KeyOf(first), provider.KeyOf(second));

        index.Add(first);
        index.Add(second);

        CollectionAssert.AreEqual(
            new[] { first },
            index[first].ToArray()
        );
        CollectionAssert.AreEqual(
            new[] { second },
            index[second].ToArray()
        );
        Assert.AreEqual(2, index.All.Count());
    }

    [TestMethod]
    public void KeyProvider_DistinguishesEveryDefaultGroupingDimension()
    {
        var db = SolverTestScenario.DB;
        var swift = "Swift".ToStandardPassive(db);
        var baseline = new TestPalReference(
            "Katress".ToPal(db),
            PalGender.MALE,
            [swift],
            new IV_Set()
        );
        var differentPal = new TestPalReference(
            "Wixen".ToPal(db),
            baseline.Gender,
            baseline.EffectivePassives,
            baseline.IVs
        );
        var differentGender = new TestPalReference(
            baseline.Pal,
            PalGender.FEMALE,
            baseline.EffectivePassives,
            baseline.IVs
        );
        var differentPassives = new TestPalReference(
            baseline.Pal,
            baseline.Gender,
            ["Runner".ToStandardPassive(db)],
            baseline.IVs
        );
        var differentIVRelevance = new TestPalReference(
            baseline.Pal,
            baseline.Gender,
            baseline.EffectivePassives,
            new IV_Set(
                HP: new IV_Value(true, 90, 100),
                Attack: IV_Value.Random,
                Defense: IV_Value.Random
            )
        );
        var provider = DefaultEffectivePropertiesKeyProvider.Instance;
        var baselineKey = provider.KeyOf(baseline);

        Assert.AreNotEqual(baselineKey, provider.KeyOf(differentPal));
        Assert.AreNotEqual(baselineKey, provider.KeyOf(differentGender));
        Assert.AreNotEqual(baselineKey, provider.KeyOf(differentPassives));
        Assert.AreNotEqual(baselineKey, provider.KeyOf(differentIVRelevance));
    }

    [TestMethod]
    public void WildPalReference_GenderVariantPreservesStructuralPassiveIdentity()
    {
        var wild = new WildPalReference(
            "Katress".ToPal(SolverTestScenario.DB),
            guaranteedPassives: [],
            numRandomPassives: 2,
            mechanics: SolverTestScenario.DB.BreedingMechanics
        );

        var gendered = wild.WithGuaranteedGender(
            SolverTestScenario.DB,
            PalGender.MALE,
            useReverser: false
        );

        Assert.AreEqual(
            wild.EffectivePassivesHash,
            gendered.EffectivePassivesHash
        );
        Assert.AreEqual(
            DefaultEffectivePropertiesKeyProvider.Instance.KeyOf(wild).Passives,
            DefaultEffectivePropertiesKeyProvider.Instance.KeyOf(gendered).Passives
        );
    }

    private sealed class TestPalReference : IPalReference
    {
        public TestPalReference(
            Pal pal,
            PalGender gender,
            IEnumerable<PassiveSkill> effectivePassives,
            IV_Set ivs,
            int? effectivePassivesHash = null
        )
        {
            Pal = pal;
            Gender = gender;
            EffectivePassives = effectivePassives.ToList();
            EffectivePassivesHash =
                effectivePassivesHash ??
                EffectivePassives.SetHash(passive => passive.InternalName);
            IVs = ivs;
        }

        public Pal Pal { get; }
        public List<PassiveSkill> EffectivePassives { get; }
        public int EffectivePassivesHash { get; }
        public IV_Set IVs { get; }
        public List<PassiveSkill> ActualPassives => EffectivePassives;
        public PalGender Gender { get; }
        public float TimeFactor => 1;
        public IPalRefLocation Location => BredRefLocation.Instance;
        public TimeSpan BreedingEffort => TimeSpan.Zero;
        public TimeSpan SelfBreedingEffort => TimeSpan.Zero;
        public int TotalCost => 0;
        public int NumTotalBreedingSteps => 0;
        public int NumTotalEggs => 0;
        public int NumTotalWildPals => 0;
        public bool IsOutdated { get; set; }

        public IPalReference WithGuaranteedGender(
            PalDB db,
            PalGender gender,
            bool useReverser
        ) =>
            this;
    }
}
