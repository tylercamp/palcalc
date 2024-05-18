using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver;
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
                Trait1 = null;
                Trait2 = null;
                Trait3 = null;
                Trait4 = null;
            }
            else
            {
                TargetPal = new PalViewModel(underlyingSpec.Pal);

                var traitVms = underlyingSpec.RequiredTraits
                    .Select(t => new TraitViewModel(t))
                    .Concat(Enumerable.Repeat<TraitViewModel>(null, GameConstants.MaxTotalTraits - underlyingSpec.RequiredTraits.Count))
                    .ToArray();

                Trait1 = traitVms[0];
                Trait2 = traitVms[1];
                Trait3 = traitVms[2];
                Trait4 = traitVms[3];

                var optionalVms = underlyingSpec.OptionalTraits
                    .Select(t => new TraitViewModel(t))
                    .Concat(Enumerable.Repeat<TraitViewModel>(null, GameConstants.MaxTotalTraits - underlyingSpec.OptionalTraits.Count))
                    .ToArray();

                OptionalTrait1 = optionalVms[0];
                OptionalTrait2 = optionalVms[1];
                OptionalTrait3 = optionalVms[2];
                OptionalTrait4 = optionalVms[3];
            }
        }

        private PalSpecifierViewModel(PalSpecifier underlyingSpec, bool isReadOnly) : this(underlyingSpec)
        {
            IsReadOnly = isReadOnly;

            if (isReadOnly)
            {
                TargetPal = null;
                Trait1 = null;
                Trait2 = null;
                Trait3 = null;
                Trait4 = null;
                OptionalTrait1 = null;
                OptionalTrait2 = null;
                OptionalTrait3 = null;
                OptionalTrait4 = null;
            }
        }

        public ICommand DeleteCommand { get; set; }

        public bool IsReadOnly { get; }
        public bool IsDynamic => !IsReadOnly;

        private IEnumerable<TraitViewModel> RequiredTraits => new List<TraitViewModel>() { Trait1, Trait2, Trait3, Trait4 }.Where(t => t != null);
        private IEnumerable<TraitViewModel> OptionalTraits => new List<TraitViewModel>() { OptionalTrait1, OptionalTrait2, OptionalTrait3, OptionalTrait4 }.Where(t => t != null);

        private List<Trait> RequiredTraitModelObjects => RequiredTraits
            .Select(t => t.ModelObject)
            .OrderBy(mo => mo.Name)
            .DistinctBy(mo => mo.Name)
            .ToList();

        private List<Trait> OptionalTraitModelObjects => OptionalTraits
            .Select(t => t.ModelObject)
            .OrderBy(mo => mo.Name)
            .DistinctBy(mo => mo.Name)
            .ToList();

        public PalSpecifier ModelObject => TargetPal != null
            ? new PalSpecifier() { Pal = TargetPal.ModelObject, RequiredTraits = RequiredTraitModelObjects, OptionalTraits = OptionalTraitModelObjects }
            : null;

        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        [ObservableProperty]
        private PalViewModel targetPal;

        [NotifyPropertyChangedFor(nameof(Label))]
        [NotifyPropertyChangedFor(nameof(RequiredTraitsCollection))]
        [ObservableProperty]
        private TraitViewModel trait1;

        [NotifyPropertyChangedFor(nameof(RequiredTraitsCollection))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private TraitViewModel trait2;

        [NotifyPropertyChangedFor(nameof(RequiredTraitsCollection))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private TraitViewModel trait3;

        [NotifyPropertyChangedFor(nameof(RequiredTraitsCollection))]
        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private TraitViewModel trait4;

        public TraitCollectionViewModel RequiredTraitsCollection => new TraitCollectionViewModel(RequiredTraits);

        [NotifyPropertyChangedFor(nameof(OptionalTraitsCollection))]
        [ObservableProperty]
        private TraitViewModel optionalTrait1;

        [NotifyPropertyChangedFor(nameof(OptionalTraitsCollection))]
        [ObservableProperty]
        private TraitViewModel optionalTrait2;

        [NotifyPropertyChangedFor(nameof(OptionalTraitsCollection))]
        [ObservableProperty]
        private TraitViewModel optionalTrait3;

        [NotifyPropertyChangedFor(nameof(OptionalTraitsCollection))]
        [ObservableProperty]
        private TraitViewModel optionalTrait4;

        public TraitCollectionViewModel OptionalTraitsCollection => new TraitCollectionViewModel(OptionalTraits);

        [ObservableProperty]
        private BreedingResultListViewModel currentResults;

        [ObservableProperty]
        private string palSourceId;

        [ObservableProperty]
        private bool includeBasePals = true;

        public bool IsValid => TargetPal != null;

        public string Label
        {
            get
            {
                if (TargetPal == null) return "New";
                else return ModelObject.ToString();
            }
        }

        public override bool Equals(object obj)
        {
            var psvm = obj as PalSpecifierViewModel;
            if (psvm == null) return false;

            return (
                psvm.TargetPal == TargetPal &&
                psvm.Trait1 == Trait1 &&
                psvm.Trait2 == Trait2 &&
                psvm.Trait3 == Trait3 &&
                psvm.Trait4 == Trait4 &&
                psvm.OptionalTrait1 == OptionalTrait1 &&
                psvm.OptionalTrait2 == OptionalTrait2 &&
                psvm.OptionalTrait3 == OptionalTrait3 &&
                psvm.OptionalTrait4 == OptionalTrait4 &&
                psvm.PalSourceId == PalSourceId &&
                psvm.IncludeBasePals == IncludeBasePals
            );
        }

        public override int GetHashCode() => HashCode.Combine(
            TargetPal,
            HashCode.Combine(
                Trait1,
                Trait2,
                Trait3,
                Trait4
            ),
            HashCode.Combine(
                OptionalTrait1,
                OptionalTrait2,
                OptionalTrait3,
                OptionalTrait4
            ),
            PalSourceId,
            IncludeBasePals
        );

        public PalSpecifierViewModel Copy() => new PalSpecifierViewModel(new PalSpecifier() { Pal = TargetPal.ModelObject, RequiredTraits = RequiredTraitModelObjects, OptionalTraits = OptionalTraitModelObjects })
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
                    TargetPal = new PalViewModel("Beakon".ToPal(db)),
                    Trait1 = new TraitViewModel("Runner".ToTrait(db)),
                    Trait2 = new TraitViewModel("Swift".ToTrait(db)),

                    OptionalTrait1 = new TraitViewModel("Aggressive".ToTrait(db))
                };
            }
        }
    }
}
