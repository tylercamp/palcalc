using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    public class BreedableParentViewModel(CachedSaveGame source, GameSettings settings, Pal pal, PalInstance instance)
    {
        private SpecificPalRefLocationViewModel location;

        public PalViewModel Pal { get; } = PalViewModel.Make(pal);

        // a parent which needs to be bred has no specific pal
        public bool IsOwned => instance != null;

        public PalGenderViewModel Gender { get; } = instance == null ? null : PalGenderViewModel.Make(instance.Gender);

        public IVSetViewModel IVs { get; } = instance == null ? null : new(
            HP: new IVDirectValueViewModel(true, instance.IV_HP),
            Attack: new IVDirectValueViewModel(true, instance.IV_Attack),
            Defense: new IVDirectValueViewModel(true, instance.IV_Defense)
        );

        // (only used by the hover popup, so these aren't created for every card up front)
        public SpecificPalRefLocationViewModel Location => instance == null ? null : location ??= new(
            source,
            settings,
            new OwnedRefLocation() { OwnerId = instance.OwnerPlayerId, Location = instance.Location }
        );
    }

    public class BreedableCardViewModel(PalViewModel pal, BreedableParentViewModel parent1, BreedableParentViewModel parent2)
    {
        public PalViewModel Pal { get; } = pal;
        public BreedableParentViewModel Parent1 { get; } = parent1;
        public BreedableParentViewModel Parent2 { get; } = parent2;
    }

    public class BreedableGroupViewModel(ILocalizedText title, bool isExpanded, List<BreedableCardViewModel> cards)
    {
        public ILocalizedText Title { get; } = title;
        public bool IsExpanded { get; } = isExpanded;
        public List<BreedableCardViewModel> Cards { get; } = cards;
    }

    public partial class BreedableToolViewModel(SolverControlsViewModel solverControls) : ObservableObject, ISolverTool
    {
        // the "calculating" note only appears if a run takes long enough to notice
        private static readonly TimeSpan CalculatingNoteDelay = TimeSpan.FromMilliseconds(100);

        private int latestRunId = 0;

        public ILocalizedText Name { get; } = LocalizationCodes.LC_TOOL_FIND_BREEDABLE.Bind();

        public SolverControlsViewModel SolverControls { get; } = solverControls;

        [ObservableProperty]
        private bool isCalculating = false;

        [ObservableProperty]
        private List<BreedableGroupViewModel> groups = [];

        public async void Run(BreedingSolverSettings settings, CachedSaveGame save) =>
            await RunAsync(settings, save);

        internal async Task RunAsync(BreedingSolverSettings settings, CachedSaveGame save)
        {
            var runId = ++latestRunId;
            var calculation = Task.Run(new BreedableCalculator(settings).Calculate);

            var noteDelay = Task.Delay(CalculatingNoteDelay);
            if (await Task.WhenAny(calculation, noteDelay) == noteDelay && runId == latestRunId)
                IsCalculating = true;

            var result = await calculation;

            // (a newer run has replaced this one)
            if (runId != latestRunId) return;

            Groups = BuildGroups(result, save, settings.GameSettings);
            IsCalculating = false;
        }

        private static List<BreedableGroupViewModel> BuildGroups(
            BreedableResult result,
            CachedSaveGame save,
            GameSettings gameSettings
        )
        {
            BreedableCardViewModel ToCard(BreedableEntry entry) =>
                new(
                    PalViewModel.Make(entry.Pal),
                    entry.Source == null ? null : new(save, gameSettings, entry.Source.Parent1.Pal, entry.Parent1Instance),
                    entry.Source == null ? null : new(save, gameSettings, entry.Source.Parent2.Pal, entry.Parent2Instance)
                );

            var ownedCards = result.Entries
                .Where(e => e.Tier == 0)
                .Select(ToCard)
                .ToList();

            var notReachableCards = result.NotReachable
                .Select(p => new BreedableCardViewModel(PalViewModel.Make(p), null, null))
                .ToList();

            // (tiers which only added a gender have no entries, so they don't get a group)
            var tierGroups = result.Entries
                .Where(e => e.Tier > 0)
                .GroupBy(e => e.Tier)
                .Select(group =>
                {
                    var cards = group.Select(ToCard).ToList();
                    return new BreedableGroupViewModel(
                        LocalizationCodes.LC_BREEDABLE_GROUP_TIER.Bind(new { Tier = group.Key, NumPals = cards.Count }),
                        isExpanded: true,
                        cards
                    );
                });

            List<BreedableGroupViewModel> groups = [
                new(LocalizationCodes.LC_BREEDABLE_GROUP_OWNED.Bind(ownedCards.Count), isExpanded: false, ownedCards),
                .. tierGroups,
                new(LocalizationCodes.LC_BREEDABLE_GROUP_NOT_REACHABLE.Bind(notReachableCards.Count), isExpanded: false, notReachableCards),
            ];

            return groups.Where(g => g.Cards.Count > 0).ToList();
        }
    }
}
