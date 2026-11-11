using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel.Solver
{
    // `requiredGender` is the gender the pair needs, if known, to show when the owned pal needs a gender reverser.
    // `hint` says how to get a parent which isn't owned, if known.
    public class BreedableParentViewModel(
        CachedSaveGame source,
        GameSettings settings,
        Pal pal,
        PalInstance instance,
        PalGender? requiredGender = null,
        ILocalizedText hint = null
    )
    {
        private SpecificPalRefLocationViewModel location;

        public PalViewModel Pal { get; } = PalViewModel.Make(pal);

        // a parent which needs to be bred has no specific pal
        public bool IsOwned => instance != null;

        public bool NeedsReverser { get; } = instance != null && requiredGender is PalGender gender && !instance.Gender.HasFlag(gender);

        public PalGenderViewModel Gender { get; } = instance == null ? null : PalGenderViewModel.Make(instance.Gender);

        public ILocalizedText Hint { get; } = hint;

        // Philanthropist, which halves the breeding time (an icon on Combos By Child's cards)
        public PassiveSkillViewModel BreedSpeedPassive { get; } = PassiveSkillViewModel.Make(
            instance?.PassiveSkills?.FirstOrDefault(p => p.TrackedEffects.Any(e => e.InternalName == PassiveSkillEffect.BreedSpeed))
        );

        public IVSetViewModel IVs { get; } = instance == null ? null : new(
            HP: new IVDirectValueViewModel(true, instance.IV_HP),
            Attack: new IVDirectValueViewModel(true, instance.IV_Attack),
            Defense: new IVDirectValueViewModel(true, instance.IV_Defense)
        );

        public SpecificPalRefLocationViewModel Location => instance == null ? null : location ??= new(
            source,
            settings,
            new OwnedRefLocation() { OwnerId = instance.OwnerPlayerId, Location = instance.Location }
        );
    }

    public partial class BreedableCardViewModel(
        PalViewModel pal,
        IVSetViewModel ivs,
        BreedableParentViewModel owned,
        BreedableParentViewModel parent1,
        BreedableParentViewModel parent2
    ) : ObservableObject
    {
        public PalViewModel Pal { get; } = pal;
        public IVSetViewModel IVs { get; } = ivs;
        public BreedableParentViewModel Owned { get; } = owned;
        public BreedableParentViewModel Parent1 { get; } = parent1;
        public BreedableParentViewModel Parent2 { get; } = parent2;

        [ObservableProperty]
        private bool isHighlighted = false;
    }

    public partial class BreedableGroupViewModel(
        ILocalizedText title,
        List<BreedableCardViewModel> cards
    ) : ObservableObject
    {
        public ILocalizedText Title { get; } = title;

        // (starts empty, `BreedableToolViewModel` moves `AllCards` in here a few at a time while loading)
        public ObservableCollection<BreedableCardViewModel> Cards { get; } = [];
        internal List<BreedableCardViewModel> AllCards { get; } = cards;

        [ObservableProperty]
        private bool hasHighlightedCards = false;
    }

    public partial class BreedableToolViewModel(SolverControlsViewModel solverControls) : ObservableObject, ISolverTool
    {
        public ILocalizedText Name { get; } = LocalizationCodes.LC_TOOL_FIND_BREEDABLE.Bind();

        public SolverControlsViewModel SolverControls { get; } = solverControls;

        [ObservableProperty]
        private List<BreedableGroupViewModel> groups = [];

        [ObservableProperty]
        private string searchText = "";

        [ObservableProperty]
        private bool hasHighlightedCards = false;

        partial void OnSearchTextChanged(string value) => UpdateHighlights();

        // off by default, since the IVs aren't needed to see what's breedable and each card builds faster without them.
        // (the best-IV parents are still the ones picked, this only changes what's shown)
        [ObservableProperty]
        private bool showIVs = false;

        [ObservableProperty]
        private bool isLoading = false;

        // WPF takes a few ms to build each card, so adding all ~300 at once froze the window for over a second.
        // adding them a few at a time lets the loading animation keep moving in between.
        // (this and the two helpers below are also used by `ParentCombosToolViewModel`)
        internal const int CardsPerBatch = 4;

        // (a newer run replaces one which is still loading)
        private int latestRun = 0;

        // (async void, so that an error is thrown on the UI thread and handled like any other)
        public async void Run(BreedingSolverSettings settings, CachedSaveGame save) => await RunAsync(settings, save);

        public async Task RunAsync(BreedingSolverSettings settings, CachedSaveGame save)
        {
            var run = ++latestRun;
            IsLoading = true;

            // (otherwise reselecting the tool would build all of the previous run's cards at once, before
            // this run gets to replace them)
            Groups = [];

            // (safe off the UI thread, the calculator keeps all of its working data local)
            var result = await OffUIThread(() => new BreedableCalculator(settings).Calculate());
            if (run != latestRun) return;

            Groups = BuildGroups(result, save, settings.GameSettings);

            var numShown = 0;
            foreach (var group in Groups)
            {
                foreach (var card in group.AllCards)
                {
                    group.Cards.Add(card);
                    if (++numShown % CardsPerBatch != 0) continue;

                    await NextFrame();
                    if (run != latestRun) return;
                }
            }

            UpdateHighlights();
            IsLoading = false;
        }

        // (with no dispatcher, as in tests, these both run straight through, so a run finishes before it returns)
        private static bool OnUIThread => SynchronizationContext.Current is DispatcherSynchronizationContext;

        internal static Task<T> OffUIThread<T>(Func<T> work) =>
            OnUIThread ? Task.Run(work) : Task.FromResult(work());

        // lets WPF lay out the cards added so far before adding more
        internal static async Task NextFrame()
        {
            if (OnUIThread)
                await Dispatcher.Yield(DispatcherPriority.Background);
        }

        private void UpdateHighlights()
        {
            var search = SearchText.Trim();

            foreach (var group in Groups)
            {
                foreach (var card in group.Cards)
                    card.IsHighlighted = search.Length > 0 && card.Pal.Name.Value.Contains(search, StringComparison.CurrentCultureIgnoreCase);

                group.HasHighlightedCards = group.Cards.Any(c => c.IsHighlighted);
            }

            HasHighlightedCards = Groups.Any(g => g.HasHighlightedCards);
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
                    IVSetViewModel.FromIVs(entry.IVs),
                    entry.OwnedInstance == null ? null : new(save, gameSettings, entry.Pal, entry.OwnedInstance),
                    entry.Source == null ? null : new(save, gameSettings, entry.Source.Parent1.Pal, entry.Parent1Instance),
                    entry.Source == null ? null : new(save, gameSettings, entry.Source.Parent2.Pal, entry.Parent2Instance)
                );

            var ownedCards = result.Entries
                .Where(e => e.Tier == 0)
                .Select(ToCard)
                .ToList();

            var notReachableCards = result.NotReachable
                .Select(p => new BreedableCardViewModel(PalViewModel.Make(p), null, null, null, null))
                .ToList();

            // (a tier is the number of breeding steps. tiers which only added a gender have no entries, so they don't get a group)
            var tierGroups = result.Entries
                .Where(e => e.Tier > 0)
                .GroupBy(e => e.Tier)
                .Select(group =>
                {
                    var cards = group.Select(ToCard).ToList();
                    return new BreedableGroupViewModel(
                        LocalizationCodes.LC_BREEDABLE_GROUP_STEPS.Bind(new { StepCount = group.Key, NumPals = cards.Count }),
                        cards
                    );
                });

            List<BreedableGroupViewModel> groups = [
                new(LocalizationCodes.LC_BREEDABLE_GROUP_OWNED.Bind(ownedCards.Count), ownedCards),
                .. tierGroups,
                new(LocalizationCodes.LC_BREEDABLE_GROUP_NOT_REACHABLE.Bind(notReachableCards.Count), notReachableCards),
            ];

            return groups.Where(g => g.AllCards.Count > 0).ToList();
        }
    }
}
