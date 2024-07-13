using CommunityToolkit.Mvvm.ComponentModel;
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

                return new AllOfSearchCriteria(criteria);
            }
        }
    }
}
