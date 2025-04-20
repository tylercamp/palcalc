using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.PalDerived;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PalCalc.UI.ViewModel.Mapped
{
    public partial class PalSpecifierViewModel : ObservableObject
    {
        public PalSpecifierViewModel(string id, PalSpecifier underlyingSpec)
        {
            IsReadOnly = false;
            Id = id;

            if (underlyingSpec == null)
            {
                TargetPal = null;
                RequiredPassives = new();
                OptionalPassives = new();

                RequiredGender = PalGenderViewModel.Wildcard;
            }
            else
            {
                TargetPal = PalViewModel.Make(underlyingSpec.Pal);

                RequiredPassives = new(underlyingSpec.RequiredPassives);
                OptionalPassives = new(underlyingSpec.OptionalPassives);

                var optionalVms = underlyingSpec.OptionalPassives
                    .Select(PassiveSkillViewModel.Make)
                    .Concat(Enumerable.Repeat<PassiveSkillViewModel>(null, GameConstants.MaxTotalPassives - underlyingSpec.OptionalPassives.Count))
                    .ToArray();

                MinIv_HP = underlyingSpec.IV_HP;
                MinIv_Attack = underlyingSpec.IV_Attack;
                MinIv_Defense = underlyingSpec.IV_Defense;

                RequiredGender = PalGenderViewModel.Make(underlyingSpec.RequiredGender);
            }
        }

        private PalSpecifierViewModel(string id, PalSpecifier underlyingSpec, bool isReadOnly) : this(id, underlyingSpec)
        {
            IsReadOnly = isReadOnly;

            if (isReadOnly)
            {
                TargetPal = null;
                RequiredPassives = new();
                OptionalPassives = new();
            }
        }

        public ICommand DeleteCommand { get; set; }

        public bool IsReadOnly { get; }
        public bool IsDynamic => !IsReadOnly;

        public string Id { get; }

        public PalSpecifier ModelObject => TargetPal != null
            ? new PalSpecifier()
            {
                Pal = TargetPal.ModelObject,
                RequiredPassives = RequiredPassives.AsModelEnumerable().ToList(),
                OptionalPassives = RequiredPassives.AsModelEnumerable().ToList(),
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

        public PalSpecifierPassiveSkillCollectionViewModel RequiredPassives { get; private set; }
        public PalSpecifierPassiveSkillCollectionViewModel OptionalPassives { get; private set; }

        /* "Min IVs" are settings for the pal */

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

        /* "Max IVs" are limits / hints for the user based on what was found in the save file */

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
        private List<IPalSourceTreeSelection> palSourceSelections;

        [ObservableProperty]
        private bool includeBasePals = true;

        [ObservableProperty]
        private bool includeCustomPals = true;

        [ObservableProperty]
        private bool includeCagedPals = true;

        [ObservableProperty]
        private bool includeGlobalStoragePals = true;

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

        [ObservableProperty]
        private SolverJobViewModel latestJob;

        public PalSpecifierViewModel Copy() => new PalSpecifierViewModel(
            IsReadOnly ? Guid.NewGuid().ToString() : Id,
            new PalSpecifier()
            {
                Pal = TargetPal.ModelObject,
                RequiredPassives = RequiredPassives.AsModelEnumerable().ToList(),
                OptionalPassives = OptionalPassives.AsModelEnumerable().ToList(),
                RequiredGender = RequiredGender.Value,
                IV_HP = MinIv_HP,
                IV_Attack = MinIv_Attack,
                IV_Defense = MinIv_Defense,
            }
        ) {
            CurrentResults = CurrentResults,
            PalSourceSelections = PalSourceSelections,
            IncludeBasePals = IncludeBasePals,
            IncludeCustomPals = IncludeCustomPals,
            IncludeCagedPals = IncludeCagedPals,
            IncludeGlobalStoragePals = IncludeGlobalStoragePals,
            DeleteCommand = DeleteCommand,
            LatestJob = LatestJob,
        };

        public static readonly PalSpecifierViewModel New = new PalSpecifierViewModel(null, null, true);

        public static PalSpecifierViewModel DesignerInstance
        {
            get
            {
                var db = PalDB.LoadEmbedded();
                return new PalSpecifierViewModel(
                    Guid.NewGuid().ToString(),
                    new PalSpecifier()
                    {
                        Pal = "Beakon".ToPal(db),
                        RequiredPassives = ["Runner".ToStandardPassive(db), "Swift".ToStandardPassive(db)],
                        OptionalPassives = ["Aggressive".ToStandardPassive(db)],
                        IV_Attack = 90,
                    }
                );
            }
        }
    }
}
