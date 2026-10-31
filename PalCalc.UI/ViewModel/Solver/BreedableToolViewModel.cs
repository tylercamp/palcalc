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
using System.ComponentModel;
using System.Linq;

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
        bool isExpanded,
        List<BreedableCardViewModel> cards
    ) : ObservableObject
    {
        public ILocalizedText Title { get; } = title;
        public List<BreedableCardViewModel> Cards { get; } = cards;

        [ObservableProperty]
        private bool isExpanded = isExpanded;

        [ObservableProperty]
        private bool hasHighlightedCards = false;
    }

    public partial class BreedableToolViewModel(SolverControlsViewModel solverControls) : ObservableObject, ISolverTool
    {
        public ILocalizedText Name { get; } = LocalizationCodes.LC_TOOL_FIND_BREEDABLE.Bind();

        public SolverControlsViewModel SolverControls { get; } = solverControls;

        [NotifyPropertyChangedFor(nameof(ExpandToggleLabel))]
        [ObservableProperty]
        private List<BreedableGroupViewModel> groups = [];

        [ObservableProperty]
        private string searchText = "";

        [ObservableProperty]
        private bool hasHighlightedCards = false;

        partial void OnSearchTextChanged(string value) => UpdateHighlights();

        private bool AreAllExpanded => Groups.All(g => g.IsExpanded);

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
            if (e.PropertyName == nameof(BreedableGroupViewModel.IsExpanded))
                OnPropertyChanged(nameof(ExpandToggleLabel));
        }

        public void Run(BreedingSolverSettings settings, CachedSaveGame save)
        {
            Groups = BuildGroups(new BreedableCalculator(settings).Calculate(), save, settings.GameSettings);
            foreach (var group in Groups)
                group.PropertyChanged += Group_PropertyChanged;

            UpdateHighlights();
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
