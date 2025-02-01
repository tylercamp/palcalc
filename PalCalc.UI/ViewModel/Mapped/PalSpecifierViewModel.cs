using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PalCalc.UI.ViewModel.Mapped
{
    public partial class PalSpecifierViewModel : ObservableObject
    {
        public PalSpecifierViewModel(PalSpecifier underlyingSpec)
        {
            IsReadOnly = false;

            if (underlyingSpec == null)
            {
                TargetPal = null;
                Passive1 = null;
                Passive2 = null;
                Passive3 = null;
                Passive4 = null;

                RequiredGender = PalGenderViewModel.Wildcard;
            }
            else
            {
                TargetPal = PalViewModel.Make(underlyingSpec.Pal);

                var passiveVms = underlyingSpec.RequiredPassives
                    .Select(PassiveSkillViewModel.Make)
                    .Concat(Enumerable.Repeat<PassiveSkillViewModel>(null, GameConstants.MaxTotalPassives - underlyingSpec.RequiredPassives.Count))
                    .ToArray();

                Passive1 = passiveVms[0];
                Passive2 = passiveVms[1];
                Passive3 = passiveVms[2];
                Passive4 = passiveVms[3];

                var optionalVms = underlyingSpec.OptionalPassives
                    .Select(PassiveSkillViewModel.Make)
                    .Concat(Enumerable.Repeat<PassiveSkillViewModel>(null, GameConstants.MaxTotalPassives - underlyingSpec.OptionalPassives.Count))
                    .ToArray();

                OptionalPassive1 = optionalVms[0];
                OptionalPassive2 = optionalVms[1];
                OptionalPassive3 = optionalVms[2];
                OptionalPassive4 = optionalVms[3];

                MinIv_HP = underlyingSpec.IV_HP;
                MinIv_Attack = underlyingSpec.IV_Attack;
                MinIv_Defense = underlyingSpec.IV_Defense;

                RequiredGender = PalGenderViewModel.Make(underlyingSpec.RequiredGender);
            }
        }

        private PalSpecifierViewModel(PalSpecifier underlyingSpec, bool isReadOnly) : this(underlyingSpec)
        {
            IsReadOnly = isReadOnly;

            if (isReadOnly)
            {
                TargetPal = null;
                Passive1 = null;
                Passive2 = null;
                Passive3 = null;
                Passive4 = null;
                OptionalPassive1 = null;
                OptionalPassive2 = null;
                OptionalPassive3 = null;
                OptionalPassive4 = null;
            }
        }

        public ICommand DeleteCommand { get; set; }

        public bool IsReadOnly { get; }
        public bool IsDynamic => !IsReadOnly;

        private IEnumerable<PassiveSkillViewModel> RequiredPassives => new List<PassiveSkillViewModel>() { Passive1, Passive2, Passive3, Passive4 }.Where(t => t != null);
        private IEnumerable<PassiveSkillViewModel> OptionalPassives => new List<PassiveSkillViewModel>() { OptionalPassive1, OptionalPassive2, OptionalPassive3, OptionalPassive4 }.Where(t => t != null);

        private List<PassiveSkill> RequiredPassiveModelObjects => RequiredPassives
            .Select(t => t.ModelObject)
            .DistinctBy(mo => mo.InternalName)
            .ToList();

        private List<PassiveSkill> OptionalPassiveModelObjects => OptionalPassives
            .Select(t => t.ModelObject)
            .DistinctBy(mo => mo.InternalName)
            .ToList();

        public PalSpecifier ModelObject => TargetPal != null
            ? new PalSpecifier()
            {
                Pal = TargetPal.ModelObject,
                RequiredPassives = RequiredPassiveModelObjects,
                OptionalPassives = OptionalPassiveModelObjects,
                RequiredGender = RequiredGender.Value,
                IV_HP = MinIv_HP,
                IV_Attack = MinIv_Attack,
                IV_Defense = MinIv_Defense,
            }
            : null;

        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        [ObservableProperty]
        private PalViewModel targetPal;

        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(RequiredPassivesCollection))]
        [ObservableProperty]
        private PassiveSkillViewModel passive1;

        [NotifyPropertyChangedFor(nameof(RequiredPassivesCollection))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private PassiveSkillViewModel passive2;

        [NotifyPropertyChangedFor(nameof(RequiredPassivesCollection))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private PassiveSkillViewModel passive3;

        [NotifyPropertyChangedFor(nameof(RequiredPassivesCollection))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private PassiveSkillViewModel passive4;

        public PassiveSkillCollectionViewModel RequiredPassivesCollection => new PassiveSkillCollectionViewModel(RequiredPassives);

        [NotifyPropertyChangedFor(nameof(OptionalPassivesCollection))]
        [ObservableProperty]
        private PassiveSkillViewModel optionalPassive1;

        [NotifyPropertyChangedFor(nameof(OptionalPassivesCollection))]
        [ObservableProperty]
        private PassiveSkillViewModel optionalPassive2;

        [NotifyPropertyChangedFor(nameof(OptionalPassivesCollection))]
        [ObservableProperty]
        private PassiveSkillViewModel optionalPassive3;

        [NotifyPropertyChangedFor(nameof(OptionalPassivesCollection))]
        [ObservableProperty]
        private PassiveSkillViewModel optionalPassive4;

        public PassiveSkillCollectionViewModel OptionalPassivesCollection => new PassiveSkillCollectionViewModel(OptionalPassives);

        [NotifyPropertyChangedFor(nameof(HasIVs))]
        [NotifyPropertyChangedFor(nameof(Iv_HP_IsValid))]
        [ObservableProperty]
        private int minIv_HP;

        [NotifyPropertyChangedFor(nameof(HasIVs))]
        [NotifyPropertyChangedFor(nameof(Iv_Attack_IsValid))]
        [ObservableProperty]
        private int minIv_Attack;

        [NotifyPropertyChangedFor(nameof(HasIVs))]
        [NotifyPropertyChangedFor(nameof(Iv_Defense_IsValid))]
        [ObservableProperty]
        private int minIv_Defense;

        [NotifyPropertyChangedFor(nameof(Iv_HP_IsValid))]
        [ObservableProperty]
        private int maxIv_HP;

        [NotifyPropertyChangedFor(nameof(Iv_Attack_IsValid))]
        [ObservableProperty]
        private int maxIv_Attack;

        [NotifyPropertyChangedFor(nameof(Iv_Defense_IsValid))]
        [ObservableProperty]
        private int maxIv_Defense;

        public bool Iv_HP_IsValid => MinIv_HP <= MaxIv_HP;
        public bool Iv_Attack_IsValid => MinIv_Attack <= MaxIv_Attack;
        public bool Iv_Defense_IsValid => MinIv_Defense <= MaxIv_Defense;

        [ObservableProperty]
        private BreedingResultListViewModel currentResults;

        [ObservableProperty]
        private PalGenderViewModel requiredGender;

        [ObservableProperty]
        private string palSourceId;

        [ObservableProperty]
        private bool includeBasePals = true;

        [ObservableProperty]
        private bool includeCustomPals = true;

        [ObservableProperty]
        private bool includeCagedPals = true;

        public bool HasIVs => MinIv_HP > 0 || MinIv_Attack > 0 || MinIv_Defense > 0;

        public bool IsValid => TargetPal != null;

        public void RefreshWith(IEnumerable<PalInstance> availablePals)
        {
            if (availablePals.Any())
            {
                MaxIv_HP = availablePals.Max(p => p.IV_HP);
                MaxIv_Attack = availablePals.Max(p => p.IV_Attack);
                MaxIv_Defense = availablePals.Max(p => p.IV_Defense);
            }
            else
            {
                MaxIv_HP = 0;
                MaxIv_Attack = 0;
                MaxIv_Defense = 0;
            }
        }

        private static ILocalizedText newTargetLabel;
        public ILocalizedText Label
        {
            get
            {
                if (TargetPal == null)
                {
                    return newTargetLabel ??= LocalizationCodes.LC_NEW_TARGET_PAL.Bind();
                }
                else
                {
                    return TargetPal.Name;
                }
            }
        }

        public PalSpecifierViewModel Copy() => new PalSpecifierViewModel(
            new PalSpecifier()
            {
                Pal = TargetPal.ModelObject,
                RequiredPassives = RequiredPassiveModelObjects,
                OptionalPassives = OptionalPassiveModelObjects,
                RequiredGender = RequiredGender.Value,
                IV_HP = MinIv_HP,
                IV_Attack = MinIv_Attack,
                IV_Defense = MinIv_Defense,
            }
        ) {
            CurrentResults = CurrentResults,
            PalSourceId = PalSourceId,
            IncludeBasePals = IncludeBasePals,
            IncludeCustomPals = IncludeCustomPals,
            IncludeCagedPals = IncludeCagedPals,
            DeleteCommand = DeleteCommand,
        };

        public static readonly PalSpecifierViewModel New = new PalSpecifierViewModel(null, true);

        public static PalSpecifierViewModel DesignerInstance
        {
            get
            {
                var db = PalDB.LoadEmbedded();
                return new PalSpecifierViewModel(null)
                {
                    TargetPal = PalViewModel.Make("Beakon".ToPal(db)),
                    Passive1 = PassiveSkillViewModel.Make("Runner".ToStandardPassive(db)),
                    Passive2 = PassiveSkillViewModel.Make("Swift".ToStandardPassive(db)),

                    OptionalPassive1 = PassiveSkillViewModel.Make("Aggressive".ToStandardPassive(db)),

                    MinIv_Attack = 90,
                };
            }
        }
    }
}
