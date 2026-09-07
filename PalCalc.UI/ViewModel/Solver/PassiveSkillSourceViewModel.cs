using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    public partial class PassiveSkillSourceViewModel : ObservableObject
    {
        private static class WarningTexts
        {
            public static readonly ILocalizedText NoneOwned = new HardCodedText("None of your Pals have this passive");
            public static readonly ILocalizedText OwnedOutOfReach = new HardCodedText("Your Pals with this passive can't reach this target within the max breeding steps");
            public static readonly ILocalizedText RequiresSameType = new HardCodedText("The target Pal can only use other Pals of the same type; none of those Pals have this passive");
            public static readonly ILocalizedText WildDisabled = new HardCodedText("This passive is available from Wild Pals, but Wild Pals are disabled");
            public static readonly ILocalizedText NoWildCarrier = new HardCodedText("No available wild Pal has this passive");
            public static readonly ILocalizedText SurgeryBanned = new HardCodedText("Surgery is not allowed for this passive");
            public static readonly ILocalizedText SurgeryTooExpensive = new HardCodedText("Surgery for this passive exceeds the max gold cost");
        }

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

            ILocalizedText OwnedRouteWarning(PassiveSkill passive)
            {
                if (!ownedPalsByPassive.TryGetValue(passive, out var carriers))
                    return targetRequiresSameType
                        ? WarningTexts.RequiresSameType
                        : WarningTexts.NoneOwned;

                var minSteps = carriers.Min(BreedingSteps);
                if (minSteps == PalBreedingDB.NotReachableBreedingSteps)
                    return WarningTexts.RequiresSameType;
                if (minSteps > solverControls.MaxBreedingSteps)
                    return WarningTexts.OwnedOutOfReach;

                return null;
            }

            ILocalizedText WildRouteWarning(PassiveSkill passive)
            {
                if (!reachableWildPassives.Contains(passive))
                    return WarningTexts.NoWildCarrier;
                if (solverControls.MaxWildPals == 0)
                    return WarningTexts.WildDisabled;

                return null;
            }

            ILocalizedText SurgeryRouteWarning(PassiveSkill passive)
            {
                if (solverControls.BannedSurgeryPassives.Contains(passive))
                    return WarningTexts.SurgeryBanned;
                if (passive.SurgeryCost > solverControls.MaxGoldCost)
                    return WarningTexts.SurgeryTooExpensive;

                return null;
            }

            return PassiveSkillViewModel.All.Select(passive =>
            {
                var model = passive.ModelObject;
                var routeWarnings = new List<ILocalizedText>
                {
                    OwnedRouteWarning(model),
                };

                if (model is not UnrecognizedPassiveSkill)
                {
                    routeWarnings.Add(WildRouteWarning(model));

                    if (model.SupportsSurgery)
                        routeWarnings.Add(SurgeryRouteWarning(model));
                }

                return new AvailablePassiveSkillViewModel(
                    passive,
                    routeWarnings.SkipNull().ToList()
                );
            }).ToList();
        }
    }
}
