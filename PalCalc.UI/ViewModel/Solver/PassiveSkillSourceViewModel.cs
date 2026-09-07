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
                specifier.PropertyChanged += Specifier_PropertyChanged;

            Recompute();
        }

        [ObservableProperty]
        private List<AvailablePassiveSkillViewModel> passives = new();

        private void SourcePals_PropertyChanged(object sender, PropertyChangedEventArgs e) => Recompute();

        private void PassiveSkills_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Recompute();

        private void Specifier_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalSpecifierViewModel.TargetPal))
                Recompute();
        }

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

            var wildPassives = db.Pals
                .Where(pal => pal != target
                    && !solverControls.BannedWildPals.Contains(pal)
                    && BreedingSteps(pal) <= solverControls.MaxBreedingSteps)
                .SelectMany(pal => pal.GuaranteedPassiveSkills(db))
                .Where(passive => passive is not (null or IUnknownPassive))
                .ToHashSet();

            return PassiveSkillViewModel.All.Select(passive =>
            {
                var model = passive.ModelObject;
                var reasons = new List<ILocalizedText>();
                var available = false;

                if (!ownedPalsByPassive.TryGetValue(model, out var ownedCarriers))
                {
                    reasons.Add(WarningTexts.NoneOwned);
                }
                else
                {
                    var ownedMinSteps = ownedCarriers.Min(BreedingSteps);
                    if (ownedMinSteps == PalBreedingDB.NotReachableBreedingSteps)
                        reasons.Add(WarningTexts.RequiresSameType);
                    else if (ownedMinSteps > solverControls.MaxBreedingSteps)
                        reasons.Add(WarningTexts.OwnedOutOfReach);
                    else
                        available = true;
                }

                if (model is not UnrecognizedPassiveSkill)
                {
                    if (!wildPassives.Contains(model))
                        reasons.Add(WarningTexts.NoWildCarrier);
                    else if (solverControls.MaxWildPals == 0)
                        reasons.Add(WarningTexts.WildDisabled);
                    else
                        available = true;

                    if (model.SupportsSurgery)
                    {
                        if (solverControls.BannedSurgeryPassives.Contains(model))
                            reasons.Add(WarningTexts.SurgeryBanned);
                        else if (model.SurgeryCost > solverControls.MaxGoldCost)
                            reasons.Add(WarningTexts.SurgeryTooExpensive);
                        else
                            available = true;
                    }
                }

                return new AvailablePassiveSkillViewModel(
                    passive,
                    available ? [] : reasons
                );
            }).ToList();
        }
    }
}
