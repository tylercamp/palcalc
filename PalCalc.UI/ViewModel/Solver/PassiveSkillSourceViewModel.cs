using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Mapped;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    public partial class PassiveSkillSourceViewModel : ObservableObject
    {
        private readonly PalSourceViewModel sourcePals;
        private readonly SolverControlsViewModel solverControls;
        private readonly PalSpecifierViewModel specifier;

        public PassiveSkillSourceViewModel(
            PalSourceViewModel sourcePals,
            SolverControlsViewModel solverControls,
            PalSpecifierViewModel specifier
        )
        {
            this.sourcePals = sourcePals;
            this.solverControls = solverControls;
            this.specifier = specifier;

            if (sourcePals != null)
                PropertyChangedEventManager.AddHandler(sourcePals, SourcePals_PropertyChanged, nameof(sourcePals.AvailablePals));

            if (solverControls != null)
                PropertyChangedEventManager.AddHandler(solverControls, SolverControls_PropertyChanged, string.Empty);

            CollectionChangedEventManager.AddHandler(PassiveSkillViewModel.All, PassiveSkills_CollectionChanged);

            if (specifier != null)
                PropertyChangedEventManager.AddHandler(specifier, Specifier_PropertyChanged, nameof(specifier.TargetPal));

            Recompute();
        }

        [ObservableProperty]
        private List<AvailablePassiveSkillViewModel> passives = new();

        private void SourcePals_PropertyChanged(object sender, PropertyChangedEventArgs e) => Recompute();

        private void PassiveSkills_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Recompute();

        private void Specifier_PropertyChanged(object sender, PropertyChangedEventArgs e) => Recompute();

        private void SolverControls_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SolverControlsViewModel.MaxBreedingSteps)
                or nameof(SolverControlsViewModel.MaxWildPals)
                or nameof(SolverControlsViewModel.BannedWildPals)
                or nameof(SolverControlsViewModel.MaxGoldCost)
                or nameof(SolverControlsViewModel.BannedSurgeryPassives))
                Recompute();
        }

        private void Recompute() => Passives = CollectPassives(
            sourcePals?.AvailablePals ?? [],
            solverControls,
            specifier
        );

        private static Dictionary<PassiveSkill, HashSet<Pal>> CollectPassivesBySourcePal(
            IEnumerable<(Pal Pal, IEnumerable<PassiveSkill> Passives)> sources
        )
        {
            var result = new Dictionary<PassiveSkill, HashSet<Pal>>();
            foreach (var (pal, passives) in sources)
            {
                foreach (var passive in passives ?? [])
                {
                    if (!result.TryGetValue(passive, out var carriers))
                        result.Add(passive, carriers = []);

                    carriers.Add(pal);
                }
            }

            return result;
        }

        internal static List<AvailablePassiveSkillViewModel> CollectPassives(
            IEnumerable<PalInstance> availablePals,
            SolverControlsViewModel solverControls,
            PalSpecifierViewModel specifier
        )
        {
            var target = specifier?.TargetPal?.ModelObject;
            var db = PalDB.LoadEmbedded();
            var breedingDB = PalBreedingDB.LoadEmbedded(db);
            var ownedPals = availablePals
                .Where(pal => pal?.Pal != null && pal.PassiveSkills != null)
                .ToList();
            var ownedPalsByPassive = CollectPassivesBySourcePal(
                ownedPals.Select(pal => (pal.Pal, pal.PassiveSkills.AsEnumerable()))
            );

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

            var reachableWildPassives = db.Pals
                .Where(pal => pal != target
                    && !solverControls.BannedWildPals.Contains(pal)
                    && BreedingSteps(pal) <= solverControls.MaxBreedingSteps)
                .SelectMany(pal => pal.GuaranteedPassiveSkills(db))
                .Where(passive => passive is not (null or IUnknownPassive))
                .ToHashSet();

            return PassiveSkillViewModel.All.Select(passive =>
            {
                var model = passive.ModelObject;
                ownedPalsByPassive.TryGetValue(model, out var ownedCarriers);
                var owned = OwnedSkillAvailabilityInfo.FromCarriers(
                    ownedCarriers,
                    BreedingSteps,
                    solverControls.MaxBreedingSteps,
                    targetRequiresSameType
                );

                var recognized = model is not UnrecognizedPassiveSkill;
                var wild = recognized
                    ? new WildSkillAvailabilityInfo(
                        reachableWildPassives.Contains(model),
                        solverControls.MaxWildPals > 0
                    )
                    : null;

                var surgery = recognized && model.SupportsSurgery
                    ? new SurgeryPassiveAvailabilityInfo(
                        !solverControls.BannedSurgeryPassives.Contains(model),
                        model.SurgeryCost <= solverControls.MaxGoldCost
                    )
                    : null;

                return new AvailablePassiveSkillViewModel(
                    passive,
                    new PassiveSkillAvailabilityInfo(owned, wild, surgery)
                );
            }).ToList();
        }
    }
}
