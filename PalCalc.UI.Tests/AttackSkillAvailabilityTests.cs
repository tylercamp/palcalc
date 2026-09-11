using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class AttackSkillAvailabilityTests
{
    private static readonly PalDB Db = PalDB.LoadEmbedded();
    private static readonly PalBreedingDB BreedingDb = PalBreedingDB.LoadEmbedded(Db);

    [TestMethod]
    public void OwnedAttackMustBeWithinBreedingStepLimit()
    {
        var source = Db.Pals.First(pal => BreedingDb.MinBreedingSteps[pal]
            .Any(pair => pair.Value > 1 && pair.Value < PalBreedingDB.NotReachableBreedingSteps));
        var target = BreedingDb.MinBreedingSteps[source]
            .First(pair => pair.Value > 1 && pair.Value < PalBreedingDB.NotReachableBreedingSteps)
            .Key;
        var attack = Db.ActiveSkills.First(candidate => candidate.CanInherit);
        var owned = new PalInstance { Pal = source, ActiveSkills = [attack] };

        var entry = EntryFor(
            AttackSkillSourceViewModel.CollectAttacks([owned], MakeControls(1), Targeting(target)),
            attack
        );

        Assert.IsTrue(entry.Availability.Owned.HasCarrier);
        Assert.IsFalse(entry.Availability.Owned.IsWithinBreedingLimit);
        Assert.IsFalse(entry.IsAvailable);
        StringAssert.Contains(entry.WarningText.Value, "max breeding steps");
    }

    [TestMethod]
    public void SameTypeTargetUsesSameTypeWarningWithoutOwnedCarrier()
    {
        var target = Db.Pals.First(candidate => Db.Pals
            .Where(source => source != candidate)
            .All(source => BreedingDb.MinBreedingSteps[source][candidate]
                == PalBreedingDB.NotReachableBreedingSteps));
        var attack = Db.ActiveSkills.First(candidate => candidate.CanInherit);

        var entry = EntryFor(
            AttackSkillSourceViewModel.CollectAttacks([], MakeControls(), Targeting(target)),
            attack
        );

        Assert.IsTrue(entry.Availability.Owned.TargetRequiresSameType);
        Assert.IsFalse(entry.IsAvailable);
        StringAssert.Contains(entry.WarningText.Value, "only use other Pals of the same type");

        var matchingCarrier = new PalInstance { Pal = target, ActiveSkills = [attack] };
        Assert.IsTrue(EntryFor(
            AttackSkillSourceViewModel.CollectAttacks([matchingCarrier], MakeControls(), Targeting(target)),
            attack
        ).IsAvailable);
    }

    [TestMethod]
    public void WildMinimumLevelAttackHonorsWildLimitAndAllowedSpecies()
    {
        var route = Db.Pals
            .Where(carrier => carrier.MinWildLevel.HasValue)
            .SelectMany(carrier => carrier.AttackLeveling(Db)
                .Where(entry => entry.Level > 1 && entry.Level <= carrier.MinWildLevel.GetValueOrDefault())
                .Select(entry => entry.Attack)
                .Where(attack => attack.CanInherit)
                .SelectMany(attack => BreedingDb.MinBreedingSteps[carrier]
                    .Where(pair => pair.Key != carrier
                        && pair.Value < PalBreedingDB.NotReachableBreedingSteps)
                    .Select(pair => (Carrier: carrier, Attack: attack, Target: pair.Key, Steps: pair.Value))))
            .First();
        var controls = MakeControls(route.Steps);
        controls.MaxWildPals = 1;

        Assert.IsTrue(EntryFor(
            AttackSkillSourceViewModel.CollectAttacks([], controls, Targeting(route.Target)),
            route.Attack
        ).IsAvailable);

        controls.MaxWildPals = 0;
        var disabledEntry = EntryFor(
            AttackSkillSourceViewModel.CollectAttacks([], controls, Targeting(route.Target)),
            route.Attack
        );
        Assert.IsFalse(disabledEntry.IsAvailable);
        StringAssert.Contains(disabledEntry.WarningText.Value, "No Pals know this attack");
        StringAssert.Contains(disabledEntry.WarningText.Value, "Wild Pals are disabled");

        controls.MaxWildPals = 1;
        controls.BannedWildPals = Db.Pals
            .Where(pal => pal.WildActiveSkills(Db).Contains(route.Attack))
            .ToList();
        var bannedEntry = EntryFor(
            AttackSkillSourceViewModel.CollectAttacks([], controls, Targeting(route.Target)),
            route.Attack
        );
        Assert.IsFalse(bannedEntry.IsAvailable);
        StringAssert.Contains(bannedEntry.WarningText.Value, "No available wild Pal");
    }

    [TestMethod]
    public void NonInheritableAttackExposesFilteringFact()
    {
        var attack = Db.ActiveSkills.First(candidate => !candidate.CanInherit);
        var entry = EntryFor(
            AttackSkillSourceViewModel.CollectAttacks([], MakeControls(), Targeting(Db.Pals.First())),
            attack
        );

        Assert.IsFalse(entry.CanInherit);
        Assert.IsFalse(entry.IsAvailable);
        StringAssert.Contains(entry.WarningText.Value, "Cannot be inherited");
    }

    private static SolverControlsViewModel MakeControls(int maxBreedingSteps = 1) => new(
        null, null, null, null, null
    )
    {
        MaxBreedingSteps = maxBreedingSteps,
        MaxWildPals = 0,
        BannedWildPals = [],
    };

    private static PalSpecifierViewModel Targeting(Pal pal) => new(
        "test-target",
        new PalSpecifier { Pal = pal }
    );

    private static AvailableAttackSkillViewModel EntryFor(
        IEnumerable<AvailableAttackSkillViewModel> entries,
        ActiveSkill attack
    ) => entries.Single(entry => entry.Attack.ModelObject.Equals(attack));
}
