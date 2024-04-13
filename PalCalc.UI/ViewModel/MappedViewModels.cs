using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public string Label { get; }
    }

    public class PalViewModel
    {
        public PalViewModel(Pal pal)
        {
            ModelObject = pal;
        }

        public Pal ModelObject { get; }

        public string Label => ModelObject == null ? "" : $"{ModelObject.Name} (#{ModelObject.Id})";

        public static readonly PalViewModel None = new PalViewModel(null);

        public override bool Equals(object obj) => (obj as PalViewModel)?.Label == Label;
        public override int GetHashCode() => Label.GetHashCode();
    }

    public class TraitViewModel
    {
        public TraitViewModel(Trait trait)
        {
            ModelObject = trait;
        }

        public Trait ModelObject { get; }

        public string Name => ModelObject?.Name ?? "None";

        public static readonly TraitViewModel None = new TraitViewModel(null);

        public override bool Equals(object obj) => (obj as TraitViewModel)?.Name == Name;
        public override int GetHashCode() => Name.GetHashCode();
    }

    public partial class PalSpecifierViewModel : ObservableObject
    {
        public PalSpecifierViewModel() : this(null) { }

        public PalSpecifierViewModel(PalSpecifier underlyingSpec)
        {
            IsReadOnly = false;

            if (underlyingSpec == null)
            {
                TargetPal = PalViewModel.None;
                Trait1 = TraitViewModel.None;
                Trait2 = TraitViewModel.None;
                Trait3 = TraitViewModel.None;
                Trait4 = TraitViewModel.None;
            }
            else
            {
                TargetPal = new PalViewModel(underlyingSpec.Pal);

                var traitVms = underlyingSpec.Traits
                    .Select(t => new TraitViewModel(t))
                    .Concat(Enumerable.Repeat(TraitViewModel.None, GameConstants.MaxTotalTraits - underlyingSpec.Traits.Count))
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
            .Select(t => t.ModelObject)
            .Where(mo => mo != null)
            .OrderBy(mo => mo.Name)
            .ToList();

        public PalSpecifier ModelObject => TargetPal != null
            ? new PalSpecifier() { Pal = TargetPal.ModelObject, Traits = TraitModelObjects }
            : null;

        [ObservableProperty]
        private PalViewModel targetPal;

        [ObservableProperty]
        private TraitViewModel trait1;
        [ObservableProperty]
        private TraitViewModel trait2;
        [ObservableProperty]
        private TraitViewModel trait3;
        [ObservableProperty]
        private TraitViewModel trait4;

        public string Label
        {
            get
            {
                if (TargetPal == null) return "New";
                else return ModelObject.ToString();
            }
        }

        public PalSpecifierViewModel Copy() => new PalSpecifierViewModel(new PalSpecifier() { Pal = TargetPal.ModelObject, Traits = TraitModelObjects });

        public static readonly PalSpecifierViewModel New = new PalSpecifierViewModel(null, true);
    }
}
