using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PalCalc.UI.ViewModel
{
    internal partial class SavesLocationViewModel
    {
        public SavesLocationViewModel(SavesLocation sl)
        {
            Value = sl;

            Label = $"{sl.ValidSaveGames.Count()} valid saves ({sl.FolderName})";
            SaveGames = sl.ValidSaveGames.Select(sg => new SaveGameViewModel(sg)).ToList();
            LastModified = SaveGames.OrderByDescending(sg => sg.LastModified).FirstOrDefault()?.LastModified;
        }

        public DateTime? LastModified { get; }

        public List<SaveGameViewModel> SaveGames { get; }

        public SavesLocation Value { get; }
        public string Label { get; }
    }

    internal partial class SaveGameViewModel
    {
        public SaveGameViewModel(SaveGame value)
        {
            Value = value;

            try
            {
                var meta = value.LevelMeta.ReadGameOptions();
                Label = $"{meta.PlayerName} lv. {meta.PlayerLevel} in {meta.WorldName}";
            }
            catch (Exception ex)
            {
                // TODO - log exception
                Label = $"{value.FolderName} (Unable to read metadata)";
            }
        }

        public DateTime LastModified => Value.LastModified;

        public SaveGame Value { get; }
        public CachedSaveGame CachedValue => Storage.LoadSave(Value, PalDB.LoadEmbedded());
        public string Label { get; }
    }

    public class PalViewModel
    {
        public PalViewModel(Pal pal)
        {
            ModelObject = pal;
        }

        public Pal ModelObject { get; }

        public ImageSource Icon => PalIcon.Images[ModelObject];

        public string Label => ModelObject == null ? "" : $"{ModelObject.Name} (#{ModelObject.Id})";

        public override bool Equals(object obj) => ModelObject.Equals((obj as PalViewModel)?.ModelObject);
        public override int GetHashCode() => ModelObject.GetHashCode();
    }

    public class PalLocationViewModel
    {
        public PalLocationViewModel(IPalRefLocation location)
        {
            ModelObject = location;
        }

        public IPalRefLocation ModelObject { get; }

        public string Description => ModelObject.ToString();
    }

    public class TraitViewModel
    {
        public static Dictionary<int, Color> RankColors = new Dictionary<int, Color>()
        {
            { -3, new Color() { R = 247, G = 63, B = 63, A = 255 } },
            { -2, new Color() { R = 247, G = 63, B = 63, A = 255 } },
            { -1, new Color() { R = 247, G = 63, B = 63, A = 255 } },
            { 0, new Color() { R = 230, G = 231, B = 223, A = 255 } },
            { 1, new Color() { R = 230, G = 231, B = 223, A = 255 } },
            { 2, new Color() { R = 255, G = 221, B = 0, A = 255 } },
            { 3, new Color() { R = 255, G = 221, B = 0, A = 255 } },
        };

        public TraitViewModel(Trait trait)
        {
            ModelObject = trait;
        }

        public Trait ModelObject { get; }

        public ImageSource RankIcon => TraitIcon.Images[ModelObject.Rank];
        public Color RankColor => RankColors[ModelObject.Rank];

        public string Name => ModelObject?.Name ?? "None";

        public override bool Equals(object obj) => ModelObject.Equals((obj as TraitViewModel)?.ModelObject);
        public override int GetHashCode() => ModelObject.GetHashCode();
    }

    public partial class PalSpecifierViewModel : ObservableObject
    {
        public PalSpecifierViewModel() : this(null) { }

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

                var traitVms = underlyingSpec.Traits
                    .Select(t => new TraitViewModel(t))
                    .Concat(Enumerable.Repeat<TraitViewModel>(null, GameConstants.MaxTotalTraits - underlyingSpec.Traits.Count))
                    .ToArray();

                Trait1 = traitVms[0];
                Trait2 = traitVms[1];
                Trait3 = traitVms[2];
                Trait4 = traitVms[3];
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
            }
        }

        public bool IsReadOnly { get; }

        private List<Trait> TraitModelObjects => new List<TraitViewModel>() { Trait1, Trait2, Trait3, Trait4 }
            .Where(t => t != null)
            .Select(t => t.ModelObject)
            .OrderBy(mo => mo.Name)
            .ToList();

        public PalSpecifier ModelObject => TargetPal != null
            ? new PalSpecifier() { Pal = TargetPal.ModelObject, Traits = TraitModelObjects }
            : null;

        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private PalViewModel targetPal;

        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private TraitViewModel trait1;

        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private TraitViewModel trait2;

        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private TraitViewModel trait3;

        [NotifyPropertyChangedFor(nameof(Label))]
        [ObservableProperty]
        private TraitViewModel trait4;

        [ObservableProperty]
        private BreedingResultListViewModel currentResults;

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

            return psvm.TargetPal == TargetPal && psvm.Trait1 == Trait1 && psvm.Trait2 == Trait2 && psvm.Trait3 == Trait3 && psvm.Trait4 == Trait4;
        }

        public override int GetHashCode() => HashCode.Combine(
            TargetPal,
            Trait1,
            Trait2,
            Trait3,
            Trait4
        );

        public PalSpecifierViewModel Copy() => new PalSpecifierViewModel(new PalSpecifier() { Pal = TargetPal.ModelObject, Traits = TraitModelObjects }) { CurrentResults = CurrentResults };
        public void CopyFrom(PalSpecifierViewModel other)
        {
            if (IsReadOnly) throw new Exception();

            TargetPal = other.TargetPal;
            Trait1 = other.Trait1;
            Trait2 = other.Trait2;
            Trait3 = other.Trait3;
            Trait4 = other.Trait4;
            CurrentResults = other.CurrentResults;
        }

        public static readonly PalSpecifierViewModel New = new PalSpecifierViewModel(null, true);
    }
}
