using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class PassiveSkillAvailabilityTests
{
    private static readonly PalDB Db = PalDB.LoadEmbedded();
    private static readonly PalBreedingDB BreedingDb = PalBreedingDB.LoadEmbedded(Db);

    [TestMethod]
    public void OwnedPassiveMustBeWithinBreedingStepLimit()
    {
        var sourcePal = Db.Pals.First(pal =>
            BreedingDb.MinBreedingSteps[pal].Any(pair => pair.Key != pal && pair.Value > 1 && pair.Value < 10000)
        );
        var distantTarget = BreedingDb.MinBreedingSteps[sourcePal]
            .First(pair => pair.Key != sourcePal && pair.Value > 1 && pair.Value < 10000)
            .Key;
        var guaranteedPassives = Db.Pals.SelectMany(pal => pal.GuaranteedPassiveSkills(Db)).ToHashSet();
        var passive = Db.StandardPassiveSkills.First(candidate =>
            !candidate.SupportsSurgery && !guaranteedPassives.Contains(candidate)
        );
        var owned = new PalInstance { Pal = sourcePal, PassiveSkills = [passive] };
        var controls = MakeControls(maxBreedingSteps: 1);

        Assert.IsTrue(EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([owned], controls, Targeting(sourcePal)),
            passive
        ).IsAvailable);
        Assert.IsFalse(EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([owned], controls, Targeting(distantTarget)),
            passive
        ).IsAvailable);
    }

    [TestMethod]
    public void OwnedPassiveOnIncompatiblePalUsesSameTypeWarning()
    {
        var route = Db.Pals
            .SelectMany(source => BreedingDb.MinBreedingSteps[source]
                .Where(pair => pair.Value == PalBreedingDB.NotReachableBreedingSteps)
                .Select(pair => (Source: source, Target: pair.Key)))
            .First();
        var guaranteedPassives = Db.Pals.SelectMany(pal => pal.GuaranteedPassiveSkills(Db)).ToHashSet();
        var passive = Db.StandardPassiveSkills.First(candidate =>
            !candidate.SupportsSurgery && !guaranteedPassives.Contains(candidate)
        );
        var owned = new PalInstance { Pal = route.Source, PassiveSkills = [passive] };

        var entry = EntryFor(
            PassiveSkillSourceViewModel.CollectPassives(
                [owned],
                MakeControls(maxBreedingSteps: 99),
                Targeting(route.Target)
            ),
            passive
        );

        Assert.IsFalse(entry.IsAvailable);
        StringAssert.Contains(entry.WarningText.Value, "only use other Pals of the same type");
    }

    [TestMethod]
    public void SameTypeTargetUsesSameTypeWarningWithoutOwnedCarrier()
    {
        var target = Db.Pals.First(candidate => Db.Pals
            .Where(source => source != candidate)
            .All(source => BreedingDb.MinBreedingSteps[source][candidate]
                == PalBreedingDB.NotReachableBreedingSteps));
        var guaranteedPassives = Db.Pals.SelectMany(pal => pal.GuaranteedPassiveSkills(Db)).ToHashSet();
        var passive = Db.StandardPassiveSkills.First(candidate =>
            !candidate.SupportsSurgery && !guaranteedPassives.Contains(candidate)
        );

        var entry = EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([], MakeControls(), Targeting(target)),
            passive
        );

        Assert.IsFalse(entry.IsAvailable);
        StringAssert.Contains(entry.WarningText.Value, "only use other Pals of the same type");
    }

    [TestMethod]
    public void WildPassiveHonorsWildLimitAndAllowedSpecies()
    {
        var route = Db.Pals
            .SelectMany(carrier => carrier.GuaranteedPassiveSkills(Db)
                .SelectMany(passive => BreedingDb.MinBreedingSteps[carrier]
                    .Where(pair => pair.Key != carrier && pair.Value < 10000)
                    .Select(pair => (Carrier: carrier, Passive: passive, Target: pair.Key, Steps: pair.Value))))
            .First();
        var controls = MakeControls(route.Steps);
        controls.MaxWildPals = 1;
        if (route.Passive.SupportsSurgery)
            controls.BannedSurgeryPassives = [route.Passive];

        Assert.IsTrue(EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([], controls, Targeting(route.Target)),
            route.Passive
        ).IsAvailable);

        controls.MaxWildPals = 0;
        Assert.IsFalse(EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([], controls, Targeting(route.Target)),
            route.Passive
        ).IsAvailable);

        controls.MaxWildPals = 1;
        controls.BannedWildPals = Db.Pals
            .Where(pal => pal.GuaranteedPassiveSkills(Db).Contains(route.Passive))
            .ToList();
        Assert.IsFalse(EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([], controls, Targeting(route.Target)),
            route.Passive
        ).IsAvailable);
    }

    [TestMethod]
    public void SurgeryPassiveHonorsAllowedListAndGoldLimit()
    {
        var passive = Db.SurgeryPassiveSkills.First(candidate => candidate.SurgeryCost > 0);
        var controls = MakeControls();
        controls.BannedWildPals = [.. Db.Pals];
        controls.MaxGoldCost = passive.SurgeryCost;
        var target = Targeting(Db.Pals.First());

        Assert.IsTrue(EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([], controls, target),
            passive
        ).IsAvailable);

        controls.BannedSurgeryPassives = [passive];
        var bannedEntry = EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([], controls, target),
            passive
        );
        Assert.IsFalse(bannedEntry.IsAvailable);
        StringAssert.Contains(bannedEntry.WarningText.Value, "Surgery is not allowed");

        controls.BannedSurgeryPassives = [];
        controls.MaxGoldCost = passive.SurgeryCost - 1;
        var expensiveEntry = EntryFor(
            PassiveSkillSourceViewModel.CollectPassives([], controls, target),
            passive
        );
        Assert.IsFalse(expensiveEntry.IsAvailable);
        StringAssert.Contains(expensiveEntry.WarningText.Value, "Surgery for this passive exceeds the max gold cost");
    }

    [TestMethod]
    public void RelevantSettingChangeRecomputesAvailability()
    {
        var passive = Db.SurgeryPassiveSkills.First(candidate => candidate.SurgeryCost > 0);
        var controls = MakeControls();
        controls.BannedWildPals = [.. Db.Pals];
        controls.MaxGoldCost = passive.SurgeryCost - 1;
        var source = new PassiveSkillSourceViewModel(null, controls, Targeting(Db.Pals.First()));

        Assert.IsFalse(EntryFor(source.Passives, passive).IsAvailable);

        controls.MaxGoldCost = passive.SurgeryCost;

        Assert.IsTrue(EntryFor(source.Passives, passive).IsAvailable);

        controls.BannedSurgeryPassives = [passive];

        var bannedEntry = EntryFor(source.Passives, passive);
        Assert.IsFalse(bannedEntry.IsAvailable);
        StringAssert.Contains(bannedEntry.WarningText.Value, "Surgery is not allowed");
    }

    [TestMethod]
    public void NewTargetShowsWarningsBeforePalSelectionAndRecomputesAfterward()
    {
        var passive = Db.SurgeryPassiveSkills.First(candidate => candidate.SurgeryCost > 0);
        var controls = MakeControls();
        controls.BannedWildPals = [.. Db.Pals];
        controls.BannedSurgeryPassives = [passive];
        var target = new PalSpecifierViewModel("new-target", null);
        var source = new PassiveSkillSourceViewModel(null, controls, target);

        Assert.IsFalse(EntryFor(source.Passives, passive).IsAvailable);
        var initialEntries = source.Passives;

        target.TargetPal = PalViewModel.Make(Db.Pals.First());

        Assert.AreNotSame(initialEntries, source.Passives);
        Assert.IsFalse(EntryFor(source.Passives, passive).IsAvailable);
    }

    private static SolverControlsViewModel MakeControls(int maxBreedingSteps = 1) => new(
        null, null, null, null, null
    )
    {
        MaxBreedingSteps = maxBreedingSteps,
        MaxWildPals = 0,
        MaxGoldCost = 0,
        BannedWildPals = [],
        BannedSurgeryPassives = [],
    };

    private static PalSpecifierViewModel Targeting(Pal pal) => new(
        "test-target",
        new PalSpecifier { Pal = pal }
    );

    private static AvailablePassiveSkillViewModel EntryFor(
        IEnumerable<AvailablePassiveSkillViewModel> entries,
        PassiveSkill passive
    ) => entries.Single(entry => entry.Passive.ModelObject.Equals(passive));
}
