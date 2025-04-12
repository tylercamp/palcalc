using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public enum IVFilterMode
    {
        MinIVs,
        MaxIVs,
    }
    public class IVFilterModeOption(IVFilterMode value)
    {
        public IVFilterMode Value => value;
        public ILocalizedText Label { get; } = (value == IVFilterMode.MinIVs ? LocalizationCodes.LC_SAVESEARCH_SETTINGS_MIN_IVS : LocalizationCodes.LC_SAVESEARCH_SETTINGS_MAX_IVS).Bind();

        public static IVFilterModeOption MinIVs { get; } = new IVFilterModeOption(IVFilterMode.MinIVs);
        public static IVFilterModeOption MaxIVs { get; } = new IVFilterModeOption(IVFilterMode.MaxIVs);
    }

    public class GenderOption(PalGender value)
    {
        public PalGender Value => value;
        public ILocalizedText Label { get; } = value.Label();

        public static GenderOption AnyGender { get; } = new GenderOption(PalGender.WILDCARD);
        public static GenderOption Female { get; } = new GenderOption(PalGender.FEMALE);
        public static GenderOption Male { get; } = new GenderOption(PalGender.MALE);
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
                    SearchedPassive1 = null;
                    SearchedPassive2 = null;
                    SearchedPassive3 = null;
                    SearchedPassive4 = null;
                    SearchedSkill1 = null;
                    SearchedSkill2 = null;
                    SearchedSkill3 = null;
                    SearchedSkill4 = null;
                    IvFilterMode = IVFilterModeOption.MinIVs;
                    FilterIVHP = 0;
                    FilterIVAttack = 0;
                    FilterIVDefense = 0;
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
        private PassiveSkillViewModel searchedPassive1;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private PassiveSkillViewModel searchedPassive2;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private PassiveSkillViewModel searchedPassive3;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private PassiveSkillViewModel searchedPassive4;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private ActiveSkillViewModel searchedSkill1;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private ActiveSkillViewModel searchedSkill2;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private ActiveSkillViewModel searchedSkill3;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private ActiveSkillViewModel searchedSkill4;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private IVFilterModeOption ivFilterMode = IVFilterModeOption.MinIVs;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private int filterIVHP = 0;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private int filterIVAttack = 0;

        [NotifyPropertyChangedFor(nameof(AsCriteria))]
        [ObservableProperty]
        private int filterIVDefense = 0;

        public List<GenderOption> GenderOptions { get; } = [
            GenderOption.AnyGender,
            GenderOption.Male,
            GenderOption.Female,
        ];

        public List<IVFilterModeOption> IVFilterOptions { get; } = [
            IVFilterModeOption.MinIVs,
            IVFilterModeOption.MaxIVs,
        ];

        public ISearchCriteria AsCriteria
        {
            get
            {
                var criteria = new List<ISearchCriteria>();
                if (SearchedPal != null) criteria.Add(new PalSearchCriteria(SearchedPal.ModelObject));
                if (SearchedGender.Value != PalGender.WILDCARD) criteria.Add(new GenderSearchCriteria(SearchedGender.Value));

                if (SearchedPassive1 != null) criteria.Add(new PassiveSkillSearchCriteria(SearchedPassive1.ModelObject));
                if (SearchedPassive2 != null) criteria.Add(new PassiveSkillSearchCriteria(SearchedPassive2.ModelObject));
                if (SearchedPassive3 != null) criteria.Add(new PassiveSkillSearchCriteria(SearchedPassive3.ModelObject));
                if (SearchedPassive4 != null) criteria.Add(new PassiveSkillSearchCriteria(SearchedPassive4.ModelObject));

                if (SearchedSkill1 != null) criteria.Add(new ActiveSkillSearchCriteria(SearchedSkill1.ModelObject));
                if (SearchedSkill2 != null) criteria.Add(new ActiveSkillSearchCriteria(SearchedSkill2.ModelObject));
                if (SearchedSkill3 != null) criteria.Add(new ActiveSkillSearchCriteria(SearchedSkill3.ModelObject));
                if (SearchedSkill4 != null) criteria.Add(new ActiveSkillSearchCriteria(SearchedSkill4.ModelObject));

                switch (IvFilterMode.Value)
                {
                    case IVFilterMode.MinIVs:
                        criteria.Add(new CustomSearchCriteria(p => p.IV_HP >= FilterIVHP));
                        criteria.Add(new CustomSearchCriteria(p => p.IV_Attack >= FilterIVAttack));
                        criteria.Add(new CustomSearchCriteria(p => p.IV_Defense >= FilterIVDefense));
                        break;

                    case IVFilterMode.MaxIVs:
                        criteria.Add(new CustomSearchCriteria(p => p.IV_HP <= FilterIVHP));
                        criteria.Add(new CustomSearchCriteria(p => p.IV_Attack <= FilterIVAttack));
                        criteria.Add(new CustomSearchCriteria(p => p.IV_Defense <= FilterIVDefense));
                        break;
                }

                return new AllOfSearchCriteria(criteria);
            }
        }

        public IRelayCommand ResetCommand { get; }
    }
}
