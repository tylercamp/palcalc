using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel
{
    public interface IPalSourceTreeNode
    {
        public ILocalizedText Label { get; }
    }

    public interface IPalSource
    {
        // a unique ID to associate solver results with a given pal source
        string Id { get; }

        public IEnumerable<PalInstance> Filter(CachedSaveGame source);
    }

    /// <summary>
    /// Filters pals based on the owner ID (for party/palbox pals) or the owner's guild ID (for base pals)
    /// </summary>
    public class PlayerSourceTreeNodeViewModel : ObservableObject, IPalSourceTreeNode, IPalSource
    {
        // TODO - handle serialization due to reference to `source` (e.g. `originalSource`)
        public PlayerSourceTreeNodeViewModel(PlayerInstance modelObject)
        {
            ModelObject = modelObject;

            Label = new HardCodedText(modelObject.Name);
        }

        public PlayerInstance ModelObject { get; }

        public string Id => $"PLAYER={ModelObject.PlayerId}";

        public ILocalizedText Label { get; }

        public IEnumerable<PalInstance> Filter(CachedSaveGame source)
        {
            var guildId = source.GuildsByPlayerId[ModelObject.PlayerId]?.Id;

            var bases = source.BasesByGuildId.GetValueOrDefault(guildId, []);

            var baseContainerIds = bases.Select(b => b.Container.Id).ToList();
            var cageContainerIds = source.PalContainers
                .OfType<ViewingCageContainer>()
                .Where(c => bases.Any(b => b.Id == c.BaseId))
                .Select(c => c.Id)
                .ToList();

            return source.OwnedPals
                .Where(p => p.Location.Type switch
                {
                    LocationType.PlayerParty => p.OwnerPlayerId == ModelObject.PlayerId,
                    LocationType.Palbox => p.OwnerPlayerId == ModelObject.PlayerId,
                    LocationType.Base => baseContainerIds.Contains(p.Location.ContainerId),
                    LocationType.ViewingCage => cageContainerIds.Contains(p.Location.ContainerId),
                    // (Pals in custom containers aren't expected to have an "owner", and are handled separately in MainWindowViewModel anyway)
                    LocationType.Custom => false,
                    _ => throw new NotImplementedException()
                });
        }
    }

    public class AnyPlayerInGuildSourceTreeNodeViewModel : ObservableObject, IPalSourceTreeNode, IPalSource
    {
        public AnyPlayerInGuildSourceTreeNodeViewModel(GuildInstance guild)
        {
            ModelObject = guild;
        }

        public GuildInstance ModelObject { get; }

        public string Id => $"GUILD={ModelObject.Id}";

        public ILocalizedText Label { get; } = LocalizationCodes.LC_PAL_SRC_ANY_GUILD_MEMBER.Bind();

        public IEnumerable<PalInstance> Filter(CachedSaveGame source)
        {
            return source.OwnedPals.Where(p => source.GuildsByPlayerId[p.OwnerPlayerId].Id == ModelObject.Id);
        }
    }


    public class GuildSourceTreeNodeViewModel : ObservableObject, IPalSourceTreeNode
    {
        public GuildSourceTreeNodeViewModel(CachedSaveGame source, GuildInstance guild)
        {
            ModelObject = guild;

            Children = new List<IPalSource>() { new AnyPlayerInGuildSourceTreeNodeViewModel(guild) };
            Children.AddRange(guild.MemberIds.Select(pid => new PlayerSourceTreeNodeViewModel(source.PlayersById[pid])).OrderBy(n => n.ModelObject.Name));

            Label = new HardCodedText(ModelObject.Name);
        }

        public GuildInstance ModelObject { get; }

        public List<IPalSource> Children { get; }

        public ILocalizedText Label { get; }
    }

    public class AnyPlayerInAnyGuildTreeNodeViewModel : ObservableObject, IPalSourceTreeNode, IPalSource
    {
        public string Id => "ANY";

        public ILocalizedText Label { get; } = LocalizationCodes.LC_PAL_SRC_ANY_PLAYER_GUILD.Bind();

        public IEnumerable<PalInstance> Filter(CachedSaveGame source) => source.OwnedPals;
    }



    public partial class PalSourceTreeViewModel : ObservableObject
    {
        // for XAML designer view
        public PalSourceTreeViewModel() : this(CachedSaveGame.SampleForDesignerView)
        {

        }

        public PalSourceTreeViewModel(CachedSaveGame save)
        {
            AvailableGuilds = new List<GuildViewModel>()
            {
                GuildViewModel.Any
            };

            AvailableGuilds.AddRange(save.Guilds.Select(g => new GuildViewModel(save, g)));

            var anySource = new AnyPlayerInAnyGuildTreeNodeViewModel();
            RootNodes = new List<IPalSourceTreeNode>() { anySource };
            RootNodes.AddRange(save.Guilds.Select(g => new GuildSourceTreeNodeViewModel(save, g)));

            SelectedNode = anySource;
        }

        private void TraverseSources(IPalSourceTreeNode node, Action<IPalSource> action)
        {
            switch (node)
            {
                case GuildSourceTreeNodeViewModel guildNode: guildNode.Children.ForEach(action); break;
                case IPalSource palSource: action(palSource); break;
                default: throw new InvalidOperationException();
            }
        }

        public IPalSource FindById(string id)
        {
            IPalSource res = null;
            foreach (var n in RootNodes)
                TraverseSources(n, s => { if (s.Id == id) res = s; });
            return res;
        }

        [NotifyPropertyChangedFor(nameof(SelectedSource))]
        [NotifyPropertyChangedFor(nameof(HasValidSource))]
        [ObservableProperty]
        private IPalSourceTreeNode selectedNode;

        public IPalSource SelectedSource => SelectedNode as IPalSource;

        public bool HasValidSource => SelectedNode is IPalSource;

        public List<IPalSourceTreeNode> RootNodes { get; }

        public List<GuildViewModel> AvailableGuilds { get; }
    }
}
