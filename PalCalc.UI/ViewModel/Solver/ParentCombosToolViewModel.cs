using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Solver
{
    public enum ParentComboSort
    {
        HighestIVs,
        AToZ,
        ZToA,
        HighestHP,
        HighestAttack,
        HighestDefense,
        // the most parents with Philanthropist, which also prefers owned pals with it
        MostPhilanthropist,
    }

    public class ParentComboSortOption(ParentComboSort sort, ILocalizedText label)
    {
        public ParentComboSort Sort { get; } = sort;
        public ILocalizedText Label { get; } = label;
    }

    // parents are shown in alphabetical order, so `A to Z` reads left to right
    public class ParentComboCardViewModel(
        ParentCombo combo,
        BreedableParentViewModel parent1,
        BreedableParentViewModel parent2
    )
    {
        public ParentCombo Combo { get; } = combo;
        public BreedableParentViewModel Parent1 { get; } = parent1;
        public BreedableParentViewModel Parent2 { get; } = parent2;

        public IVSetViewModel IVs { get; } = combo.IVs is IV_Set ivs ? IVSetViewModel.FromIVs(ivs) : null;

        // the child's IVs when both parents are owned, otherwise the IVs of the one owned parent
        public IV_Set? SortIVs { get; } = combo.IVs ?? (
            (combo.Parent1Instance ?? combo.Parent2Instance) is PalInstance owned ? BreedableCalculator.ExactIVs(owned) : null
        );
    }

    public partial class ParentComboGroupViewModel(
        ILocalizedText title,
        int numOwnedParents,
        bool isExpanded,
        List<ParentComboCardViewModel> cards
    ) : ObservableObject
    {
        public ILocalizedText Title { get; } = title;

        public int NumOwnedParents { get; } = numOwnedParents;

        // (starts empty, `ParentCombosToolViewModel` moves `AllCards` in here a few at a time while loading)
        public ObservableCollection<ParentComboCardViewModel> Cards { get; } = [];
        internal List<ParentComboCardViewModel> AllCards { get; } = cards;

        [ObservableProperty]
        private bool isExpanded = isExpanded;
    }

    public partial class ParentCombosToolViewModel(SolverControlsViewModel solverControls) : ObservableObject, ISolverTool
    {
        public ILocalizedText Name { get; } = LocalizationCodes.LC_TOOL_COMBOS_BY_CHILD.Bind();

        public SolverControlsViewModel SolverControls { get; } = solverControls;

        public IReadOnlyList<PalViewModel> ChildOptions { get; } = PalViewModel.All
            .Where(p => p.ModelObject.InternalName != BreedableCalculator.HiddenPalInternalName)
            .ToList();

        public IReadOnlyList<ParentComboSortOption> SortOptions { get; } = [
            new(ParentComboSort.HighestIVs, LocalizationCodes.LC_COMBOS_SORT_HIGHEST_IVS.Bind()),
            new(ParentComboSort.AToZ, LocalizationCodes.LC_COMBOS_SORT_A_TO_Z.Bind()),
            new(ParentComboSort.ZToA, LocalizationCodes.LC_COMBOS_SORT_Z_TO_A.Bind()),
            new(ParentComboSort.HighestHP, LocalizationCodes.LC_COMBOS_SORT_HP.Bind()),
            new(ParentComboSort.HighestAttack, LocalizationCodes.LC_COMBOS_SORT_ATTACK.Bind()),
            new(ParentComboSort.HighestDefense, LocalizationCodes.LC_COMBOS_SORT_DEFENSE.Bind()),
            new(ParentComboSort.MostPhilanthropist, LocalizationCodes.LC_COMBOS_SORT_PHILANTHROPIST.Bind()),
        ];

        private BreedingSolverSettings settings;
        private CachedSaveGame save;
        private ParentCombosCalculator calculator;
        private BreedableResult breedable;

        [ObservableProperty]
        private bool isLoading = false;

        // (a newer run, child or sort replaces one which is still loading)
        private int latestRun = 0;
        private int latestUpdate = 0;

        [ObservableProperty]
        private PalViewModel child;

        // (kept while the app is open, the child is cleared each time the tool is selected)
        [ObservableProperty]
        private ParentComboSortOption selectedSort;

        [NotifyPropertyChangedFor(nameof(ExpandToggleLabel))]
        [ObservableProperty]
        private List<ParentComboGroupViewModel> groups = [];

        partial void OnChildChanged(PalViewModel value) => Update(keepExpanded: false);

        // (recalculates, since the Philanthropist sort changes which owned pals are picked)
        partial void OnSelectedSortChanged(ParentComboSortOption value) => Update(keepExpanded: true);

        // (shows "Expand All" while no child is chosen)
        private bool AreAllExpanded => Groups.Count > 0 && Groups.All(g => g.IsExpanded);

        public ILocalizedText ExpandToggleLabel =>
            (AreAllExpanded ? LocalizationCodes.LC_BREEDABLE_COLLAPSE_ALL : LocalizationCodes.LC_BREEDABLE_EXPAND_ALL).Bind();

        [RelayCommand]
        private void ToggleExpanded()
        {
            var isExpanded = !AreAllExpanded;
            foreach (var group in Groups)
                group.IsExpanded = isExpanded;
        }

        private void Group_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParentComboGroupViewModel.IsExpanded))
                OnPropertyChanged(nameof(ExpandToggleLabel));
        }

        public void OnSelected()
        {
            SelectedSort ??= SortOptions[0];
            Child = null;
        }

        // (async void, so that an error is thrown on the UI thread and handled like any other)
        public async void Run(BreedingSolverSettings settings, CachedSaveGame save) => await RunAsync(settings, save);

        public async Task RunAsync(BreedingSolverSettings settings, CachedSaveGame save)
        {
            var run = ++latestRun;

            // (safe off the UI thread, both calculators keep all of their working data local)
            var newBreedable = await BreedableToolViewModel.OffUIThread(() => new BreedableCalculator(settings).Calculate());
            if (run != latestRun) return;

            this.settings = settings;
            this.save = save;
            calculator = new ParentCombosCalculator(settings);
            breedable = newBreedable;

            await UpdateAsync(keepExpanded: false);
        }

        // (async void, as with `Run`)
        private async void Update(bool keepExpanded) => await UpdateAsync(keepExpanded);

        private async Task UpdateAsync(bool keepExpanded)
        {
            var update = ++latestUpdate;
            var expanded = keepExpanded ? Groups.ToDictionary(g => g.NumOwnedParents, g => g.IsExpanded) : [];

            // (cleared straight away, so the old cards are never rebuilt while the new ones load)
            SetGroups([]);

            if (Child == null || calculator == null)
            {
                IsLoading = false;
                return;
            }

            IsLoading = true;

            var child = Child.ModelObject;
            var preferBreedSpeed = SelectedSort?.Sort == ParentComboSort.MostPhilanthropist;
            var (currentCalculator, currentBreedable) = (calculator, breedable);
            var combos = await BreedableToolViewModel.OffUIThread(() => currentCalculator.Calculate(child, currentBreedable, preferBreedSpeed));
            if (update != latestUpdate) return;

            SetGroups(BuildGroups(combos, save, settings.GameSettings, SelectedSort?.Sort ?? ParentComboSort.HighestIVs, expanded));

            // (a few at a time so the loading animation keeps moving, see `BreedableToolViewModel`)
            var numShown = 0;
            foreach (var group in Groups)
            {
                foreach (var card in group.AllCards)
                {
                    group.Cards.Add(card);
                    if (++numShown % BreedableToolViewModel.CardsPerBatch != 0) continue;

                    await BreedableToolViewModel.NextFrame();
                    if (update != latestUpdate) return;
                }
            }

            IsLoading = false;
        }

        private void SetGroups(List<ParentComboGroupViewModel> groups)
        {
            foreach (var group in Groups)
                group.PropertyChanged -= Group_PropertyChanged;

            Groups = groups;

            foreach (var group in Groups)
                group.PropertyChanged += Group_PropertyChanged;
        }

        private static List<ParentComboGroupViewModel> BuildGroups(
            ParentCombosResult result,
            CachedSaveGame save,
            GameSettings gameSettings,
            ParentComboSort sort,
            Dictionary<int, bool> expanded
        )
        {
            ParentComboCardViewModel ToCard(ParentCombo combo)
            {
                BreedableParentViewModel Parent(Pal pal, PalInstance instance, ParentAvailability availability, PalGender gender) =>
                    new(save, gameSettings, pal, instance, gender, Hint(availability));

                var parent1 = Parent(combo.Source.Parent1.Pal, combo.Parent1Instance, combo.Parent1Availability, combo.Parent1Gender);
                var parent2 = Parent(combo.Source.Parent2.Pal, combo.Parent2Instance, combo.Parent2Availability, combo.Parent1Gender.OppositeGender());

                return string.Compare(parent1.Pal.Name.Value, parent2.Pal.Name.Value, StringComparison.CurrentCultureIgnoreCase) <= 0
                    ? new(combo, parent1, parent2)
                    : new(combo, parent2, parent1);
            }

            return result.Combos
                .Select(ToCard)
                .GroupBy(c => c.Combo.NumOwnedParents)
                .OrderByDescending(g => g.Key)
                .Select(group =>
                {
                    var cards = Sorted(group, sort).ToList();
                    var title = group.Key switch
                    {
                        2 => LocalizationCodes.LC_COMBOS_GROUP_READY.Bind(cards.Count),
                        1 => LocalizationCodes.LC_COMBOS_GROUP_ONE_FIRST.Bind(cards.Count),
                        _ => LocalizationCodes.LC_COMBOS_GROUP_NEITHER.Bind(cards.Count),
                    };

                    return new ParentComboGroupViewModel(
                        title,
                        group.Key,
                        expanded.GetValueOrDefault(group.Key, group.Key > 0),
                        cards
                    );
                })
                .ToList();
        }

        private static ILocalizedText Hint(ParentAvailability availability) =>
            availability switch
            {
                ParentAvailability.Breedable => LocalizationCodes.LC_COMBOS_PARENT_CAN_BE_BRED.Bind(),
                ParentAvailability.CatchOnly => LocalizationCodes.LC_COMBOS_PARENT_CATCH_ONLY.Bind(),
                ParentAvailability.NotBreedable => LocalizationCodes.LC_COMBOS_PARENT_NOT_BREEDABLE.Bind(),
                _ => null,
            };

        public static IEnumerable<ParentComboCardViewModel> Sorted(IEnumerable<ParentComboCardViewModel> cards, ParentComboSort sort)
        {
            var byName = StringComparer.CurrentCultureIgnoreCase;
            string Name(ParentComboCardViewModel c) => c.Parent1.Pal.Name.Value + " + " + c.Parent2.Pal.Name.Value;

            // the middle of each range, and pairs with no owned parent have no IVs so they sort last
            static double Middle(IV_Value iv) => (iv.Min + iv.Max) / 2.0;
            static double Average(IV_Set? ivs) =>
                ivs is IV_Set s ? (Middle(s.HP) + Middle(s.Attack) + Middle(s.Defense)) / 3 : -1;
            static double Stat(IV_Set? ivs, Func<IV_Set, IV_Value> stat) =>
                ivs is IV_Set s ? Middle(stat(s)) : -1;

            var sorted = sort switch
            {
                ParentComboSort.AToZ => cards.OrderBy(Name, byName),
                ParentComboSort.ZToA => cards.OrderByDescending(Name, byName),
                ParentComboSort.HighestHP => cards.OrderByDescending(c => Stat(c.SortIVs, s => s.HP)).ThenByDescending(c => Average(c.SortIVs)),
                ParentComboSort.HighestAttack => cards.OrderByDescending(c => Stat(c.SortIVs, s => s.Attack)).ThenByDescending(c => Average(c.SortIVs)),
                ParentComboSort.HighestDefense => cards.OrderByDescending(c => Stat(c.SortIVs, s => s.Defense)).ThenByDescending(c => Average(c.SortIVs)),
                ParentComboSort.MostPhilanthropist => cards.OrderByDescending(c => c.Combo.NumBreedSpeedParents).ThenByDescending(c => Average(c.SortIVs)),
                _ => cards.OrderByDescending(c => Average(c.SortIVs)),
            };

            // (names keep the order the same between runs)
            return sort is ParentComboSort.AToZ or ParentComboSort.ZToA ? sorted : sorted.ThenBy(Name, byName);
        }
    }
}
