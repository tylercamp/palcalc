using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Localization;
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
            ? new PalSpecifier() { Pal = TargetPal.ModelObject, RequiredPassives = RequiredPassiveModelObjects, OptionalPassives = OptionalPassiveModelObjects }
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

        [ObservableProperty]
        private BreedingResultListViewModel currentResults;

        [ObservableProperty]
        private string palSourceId;

        [ObservableProperty]
        private bool includeBasePals = true;

        public bool IsValid => TargetPal != null;

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

        public override bool Equals(object obj)
        {
            var psvm = obj as PalSpecifierViewModel;
            if (psvm == null) return false;

            return (
                psvm.TargetPal == TargetPal &&
                psvm.Passive1 == Passive1 &&
                psvm.Passive2 == Passive2 &&
                psvm.Passive3 == Passive3 &&
                psvm.Passive4 == Passive4 &&
                psvm.OptionalPassive1 == OptionalPassive1 &&
                psvm.OptionalPassive2 == OptionalPassive2 &&
                psvm.OptionalPassive3 == OptionalPassive3 &&
                psvm.OptionalPassive4 == OptionalPassive4 &&
                psvm.PalSourceId == PalSourceId &&
                psvm.IncludeBasePals == IncludeBasePals
            );
        }

        public override int GetHashCode() => HashCode.Combine(
            TargetPal,
            HashCode.Combine(
                Passive1,
                Passive2,
                Passive3,
                Passive4
            ),
            HashCode.Combine(
                OptionalPassive1,
                OptionalPassive2,
                OptionalPassive3,
                OptionalPassive4
            ),
            PalSourceId,
            IncludeBasePals
        );

        public PalSpecifierViewModel Copy() => new PalSpecifierViewModel(new PalSpecifier() { Pal = TargetPal.ModelObject, RequiredPassives = RequiredPassiveModelObjects, OptionalPassives = OptionalPassiveModelObjects })
        {
            CurrentResults = CurrentResults,
            PalSourceId = PalSourceId,
            IncludeBasePals = IncludeBasePals,
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
                    Passive1 = PassiveSkillViewModel.Make("Runner".ToPassive(db)),
                    Passive2 = PassiveSkillViewModel.Make("Swift".ToPassive(db)),

                    OptionalPassive1 = PassiveSkillViewModel.Make("Aggressive".ToPassive(db))
                };
            }
        }
    }
}
