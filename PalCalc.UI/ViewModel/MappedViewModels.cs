using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

// viewmodels which are just wrappers around PalCalc.Model types

namespace PalCalc.UI.ViewModel
{
    public interface ISavesLocationViewModel
    {
        ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }
        string Label { get; }
        DateTime? LastModified { get; }
    }

    public class StandardSavesLocationViewModel : ISavesLocationViewModel
    {
        public StandardSavesLocationViewModel(SavesLocation sl)
        {
            Value = sl;

            Label = $"{sl.ValidSaveGames.Count()} valid saves ({sl.FolderName})";
            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(
                new ObservableCollection<SaveGameViewModel>(sl.ValidSaveGames.Select(sg => new SaveGameViewModel(sg)))
            );
            LastModified = sl.ValidSaveGames.OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;
        }

        public SavesLocation Value { get; }

        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public string Label { get; }

        public DateTime? LastModified { get; }
    }

    public class ManualSavesLocationViewModel : ISavesLocationViewModel
    {
        public ManualSavesLocationViewModel(IEnumerable<SaveGame> initialManualSaves)
        {
            saveGames = new ObservableCollection<SaveGameViewModel>(initialManualSaves.Select(s => new SaveGameViewModel(s)).OrderBy(vm => vm.Label));
            saveGames.Add(SaveGameViewModel.AddNewSave);

            SaveGames = new ReadOnlyObservableCollection<SaveGameViewModel>(saveGames);
        }

        private ObservableCollection<SaveGameViewModel> saveGames;
        public ReadOnlyObservableCollection<SaveGameViewModel> SaveGames { get; }

        public string Label => "Manually Added";

        public DateTime? LastModified => saveGames.Where(g => !g.IsAddManualOption).OrderByDescending(g => g.LastModified).FirstOrDefault()?.LastModified;

        // assume `path` has already been validated
        public SaveGameViewModel Add(SaveGame saveGame)
        {
            var vm = new SaveGameViewModel(saveGame);
            var orderedIndex = saveGames
                .Where(vm => !vm.IsAddManualOption)
                .Append(vm)
                .OrderBy(vm => vm.Label)
                .ToList()
                .IndexOf(vm);

            saveGames.Insert(orderedIndex, vm);

            return vm;
        }
    }

    public partial class SaveGameViewModel
    {
        private SaveGameViewModel()
        {
            IsAddManualOption = true;
            Label = "Add a new save...";
        }

        public SaveGameViewModel(SaveGame value)
        {
            IsAddManualOption = false;
            Value = value;

            try
            {
                var meta = value.LevelMeta.ReadGameOptions();
                Label = meta.ToString();
                //Label = $"{meta.PlayerName} lv. {meta.PlayerLevel} in {meta.WorldName}";
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

        public bool IsValid => Value.IsValid;

        public bool IsAddManualOption { get; }

        public Visibility WarningVisibility => !IsAddManualOption && !IsValid ? Visibility.Visible : Visibility.Collapsed;

        public static readonly SaveGameViewModel AddNewSave = new SaveGameViewModel();
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

    public class PalRefLocationViewModel
    {
        public PalRefLocationViewModel(CachedSaveGame source, IPalRefLocation location)
        {
            ModelObject = location;

            var ownedLoc = location as OwnedRefLocation;
            if (ownedLoc == null) return;

            if (source == null)
            {
                // for XAML designer preview
                IsSinglePlayer = true;
                LocationOwner = ownedLoc.OwnerId;
            }
            else
            {
                IsSinglePlayer = source.Players.Count == 1;

                // TODO - getting wrong player ID, i.e. instance ID v player ID
                var ownerName = source.PlayersById[ownedLoc.OwnerId].Name;
                var ownerGuild = source.GuildsByPlayerId[ownedLoc.OwnerId];

                var isGuildOwner = ownedLoc.Location.Type == LocationType.Base && ownerGuild.MemberIds.Count > 1;
                LocationOwner = isGuildOwner ? ownerGuild.Name : ownerName;
            }

            switch (ownedLoc.Location.Type)
            {
                case LocationType.PlayerParty:
                    LocationCoordDescription = $"Party, slot {ownedLoc.Location.Index}";
                    break;

                case LocationType.Base:
                    var baseCoord = BaseCoord.FromSlotIndex(ownedLoc.Location.Index);
                    LocationCoordDescription = $"A base, slot ({baseCoord.X},{baseCoord.Y})";
                    break;

                case LocationType.Palbox:
                    var pboxCoord = PalboxCoord.FromSlotIndex(ownedLoc.Location.Index);
                    LocationCoordDescription = $"Palbox, tab {pboxCoord.Tab} at ({pboxCoord.X},{pboxCoord.Y})";
                    break;
            }
        }

        public bool IsSinglePlayer { get; }

        public IPalRefLocation ModelObject { get; }
        public Visibility Visibility => ModelObject is OwnedRefLocation ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OwnerVisibility => IsSinglePlayer ? Visibility.Collapsed : Visibility.Visible;

        public string LocationOwner { get; }
        public string LocationOwnerDescription => $"Owned by {LocationOwner}";
        
        public string LocationCoordDescription { get; }
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

        // for XAML designer view
        public TraitViewModel()
        {
            ModelObject = new Trait("Runner", "runner", 2);
        }

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
            .DistinctBy(mo => mo.Name)
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

    public class PlayerViewModel
    {
        public PlayerViewModel(PlayerInstance player)
        {
            ModelObject = player;
        }

        public PlayerInstance ModelObject { get; }

        public bool IsWildcard => ModelObject == null;

        public string Name => IsWildcard ? "Any Player" : ModelObject.Name;

        public static readonly PlayerViewModel Any = new PlayerViewModel(null);
    }

    public class GuildViewModel
    {
        public GuildViewModel(CachedSaveGame source, GuildInstance guild)
        {
            ModelObject = guild;

            AvailableMembers = new List<PlayerViewModel>
            {
                PlayerViewModel.Any
            };

            if (source != null)
            {
                AvailableMembers.AddRange(
                    guild.MemberIds
                        .Select(id => source.Players.Single(p => p.InstanceId == id))
                        .Select(player => new PlayerViewModel(player))
                        .ToList()
                );
            }
        }

        public GuildInstance ModelObject { get; }

        public bool IsWildcard => ModelObject == null;

        public List<PlayerViewModel> AvailableMembers { get; }

        public string Name => IsWildcard ? "Any Guild" : ModelObject.Name;

        public static readonly GuildViewModel Any = new GuildViewModel(null, null);
    }
}
