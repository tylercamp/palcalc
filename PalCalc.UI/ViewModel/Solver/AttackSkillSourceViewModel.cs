using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Mapped;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    public partial class AttackSkillSourceViewModel : ObservableObject
    {
        private readonly PalSourceViewModel sourcePals;
        private readonly SolverControlsViewModel solverControls;
        private readonly PalSpecifierViewModel specifier;

        public AttackSkillSourceViewModel(
            PalSourceViewModel sourcePals,
            SolverControlsViewModel solverControls,
            PalSpecifierViewModel specifier
        )
        {
            this.sourcePals = sourcePals;
            this.solverControls = solverControls;
            this.specifier = specifier;

            PropertyChangedEventManager.AddHandler(sourcePals, SourcePals_PropertyChanged, nameof(sourcePals.AvailablePals));
            PropertyChangedEventManager.AddHandler(solverControls, SolverControls_PropertyChanged, string.Empty);
            PropertyChangedEventManager.AddHandler(specifier, Specifier_PropertyChanged, nameof(specifier.TargetPal));

            Recompute();
        }

        private void SourcePals_PropertyChanged(object sender, PropertyChangedEventArgs args) => Recompute();
        private void Specifier_PropertyChanged(object sender, PropertyChangedEventArgs args) => Recompute();

        private void SolverControls_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(SolverControlsViewModel.MaxBreedingSteps)
                or nameof(SolverControlsViewModel.MaxWildPals)
                or nameof(SolverControlsViewModel.BannedWildPals))
                Recompute();
        }

        private void Recompute() => Attacks = CollectAttacks(
            sourcePals.AvailablePals,
            solverControls,
            specifier
        );

        internal static List<AvailableAttackSkillViewModel> CollectAttacks(
            IEnumerable<PalInstance> availablePals,
            SolverControlsViewModel solverControls,
            PalSpecifierViewModel specifier
        )
        {
            var target = specifier?.TargetPal?.ModelObject;
            var db = PalDB.LoadEmbedded();
            var breedingDB = PalBreedingDB.LoadEmbedded(db);
            var ownedPals = availablePals.Where(pal => pal?.Pal != null).ToList();
            var ownedSpecies = ownedPals.Select(pal => pal.Pal).ToHashSet();
            var carriersByAttack = ownedPals
                .SelectMany(pal => (pal.ActiveSkills ?? []).Select(attack => (Attack: attack, pal.Pal)))
                .GroupBy(pair => pair.Attack)
                .ToDictionary(group => group.Key, group => group.Select(pair => pair.Pal).ToHashSet());

            int BreedingSteps(Pal pal)
            {
                if (target == null)
                    return 0;

                return breedingDB.MinBreedingSteps.TryGetValue(pal, out var toTarget)
                    && toTarget.TryGetValue(target, out var steps)
                        ? steps
                        : PalBreedingDB.NotReachableBreedingSteps;
            }

            var targetRequiresSameType = target != null && db.Pals
                .Where(pal => pal != target)
                .All(pal => BreedingSteps(pal) == PalBreedingDB.NotReachableBreedingSteps);

            var reachableWildAttacks = db.Pals
                .Where(pal => pal != target
                    && !ownedSpecies.Contains(pal)
                    && !solverControls.BannedWildPals.Contains(pal)
                    && BreedingSteps(pal) <= solverControls.MaxBreedingSteps)
                .SelectMany(pal => pal.WildActiveSkills(db))
                .ToHashSet();

            return ActiveSkillViewModel.All.Select(attack =>
            {
                carriersByAttack.TryGetValue(attack.ModelObject, out var carriers);
                return new AvailableAttackSkillViewModel(
                    attack,
                    new AttackSkillAvailabilityInfo(
                        attack.ModelObject.CanInherit,
                        OwnedSkillAvailabilityInfo.FromCarriers(
                            carriers,
                            BreedingSteps,
                            solverControls.MaxBreedingSteps,
                            targetRequiresSameType
                        ),
                        new WildSkillAvailabilityInfo(
                            reachableWildAttacks.Contains(attack.ModelObject),
                            solverControls.MaxWildPals > 0
                        )
                    )
                );
            }).ToList();
        }

        [NotifyPropertyChangedFor(nameof(InheritableAttacks))]
        [NotifyPropertyChangedFor(nameof(AvailableAttacks))]
        [ObservableProperty]
        private List<AvailableAttackSkillViewModel> attacks;

        public IEnumerable<AvailableAttackSkillViewModel> InheritableAttacks =>
            Attacks.Where(attack => attack.CanInherit);

        public IEnumerable<ActiveSkillViewModel> AvailableAttacks =>
            Attacks.Where(a => a.IsAvailable).Select(a => a.Attack);
    }
}
