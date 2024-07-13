using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public class GenderOption
    {
        public PalGender Value { get; set; }
        public string Label => Value.Label();

        public static GenderOption AnyGender { get; } = new GenderOption() { Value = PalGender.WILDCARD };
        public static GenderOption Female { get; } = new GenderOption() { Value = PalGender.FEMALE };
        public static GenderOption Male { get; } = new GenderOption() { Value = PalGender.MALE };
    }

    public partial class SearchSettingsViewModel : ObservableObject
    {
        public SearchSettingsViewModel()
        {
            ResetCommand = new RelayCommand(
                execute: () =>
                {
                    SearchedPal = null;
                    SearchedGender = GenderOption.AnyGender;
                    SearchedTrait1 = null;
                    SearchedTrait2 = null;
                    SearchedTrait3 = null;
                    SearchedTrait4 = null;
                    MinIVHP = 0;
                    MinIVAttack = 0;
                    MinIVDefense = 0;
                }
            );
        }

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private PalViewModel searchedPal;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private GenderOption searchedGender = GenderOption.AnyGender;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private TraitViewModel searchedTrait1;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private TraitViewModel searchedTrait2;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private TraitViewModel searchedTrait3;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private TraitViewModel searchedTrait4;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private int minIVHP = 0;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private int minIVAttack = 0;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private int minIVDefense = 0;

        public List<PalViewModel> PalOptions { get; } = PalDB.LoadEmbedded().Pals.Select(p => new PalViewModel(p)).ToList();

        public List<GenderOption> GenderOptions { get; } = [
            GenderOption.AnyGender,
            GenderOption.Male,
            GenderOption.Female,
        ];

        public List<TraitViewModel> TraitOptions { get; } = PalDB.LoadEmbedded().Traits.Select(t => new TraitViewModel(t)).ToList();

        public ISearchCriteria AsCriteria
        {
            get
            {
                var criteria = new List<ISearchCriteria>();
                if (SearchedPal != null) criteria.Add(new PalSearchCriteria(SearchedPal.ModelObject));
                if (SearchedGender.Value != PalGender.WILDCARD) criteria.Add(new GenderSearchCriteria(SearchedGender.Value));

                if (SearchedTrait1 != null) criteria.Add(new TraitSearchCriteria(SearchedTrait1.ModelObject));
                if (SearchedTrait2 != null) criteria.Add(new TraitSearchCriteria(SearchedTrait2.ModelObject));
                if (SearchedTrait3 != null) criteria.Add(new TraitSearchCriteria(SearchedTrait3.ModelObject));
                if (SearchedTrait4 != null) criteria.Add(new TraitSearchCriteria(SearchedTrait4.ModelObject));

                criteria.Add(new CustomSearchCriteria(p => p.IV_HP >= MinIVHP));
                criteria.Add(new CustomSearchCriteria(p => p.IV_Shot >= MinIVAttack));
                criteria.Add(new CustomSearchCriteria(p => p.IV_Defense >= MinIVDefense));

                return new AllOfSearchCriteria(criteria);
            }
        }

        public IRelayCommand ResetCommand { get; }
    }
}
