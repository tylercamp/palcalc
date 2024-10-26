using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public interface IOwnerTreeNode
    {
        ILocalizedText Label { get; }
        List<IOwnerTreeNode> Children => [];

        public IEnumerable<IOwnerTreeNode> AllChildren => Children.Concat(Children.SelectMany(c => c.AllChildren));
    }

    public abstract class IContainerSource : ObservableObject, IOwnerTreeNode
    {
        public IContainerSource(ILocalizedText label, ContainerViewModel container)
        {
            Container = container;
            Label = label;
            SearchedLabel = Label;
        }

        public ContainerViewModel Container { get; }
        public ILocalizedText Label { get; }


        private ILocalizedText searchedLabel;
        public ILocalizedText SearchedLabel
        {
            get => searchedLabel;
            private set => SetProperty(ref searchedLabel, value);
        }

        public ISearchCriteria SearchCriteria
        {
            set
            {
                Container.SearchCriteria = value;
                SearchedLabel = LocalizationCodes.LC_SAVESEARCH_CONTAINER_LABEL.Bind(
                    new
                    {
                        Label = Label,
                        NumMatches = Container.Grids.Sum(g => g.Slots.Count(s => s.Matches)),
                    }
                );
            }
        }
    }

    public class PlayerPalboxContainerViewModel(ContainerViewModel container) :
        IContainerSource(LocalizationCodes.LC_PAL_LOC_PALBOX.Bind(), container)
    {
    }

    public class PlayerPartyContainerViewModel(ContainerViewModel container) :
        IContainerSource(LocalizationCodes.LC_PAL_LOC_PARTY.Bind(), container)
    {
    }

    public class BaseTreeNodeViewModel(ContainerViewModel baseContainer) :
        IContainerSource(LocalizationCodes.LC_BASE_LABEL.Bind(baseContainer.Id.Split('-')[0]), baseContainer)
    {
    }

    public class PlayerTreeNodeViewModel(PlayerInstance player, ContainerViewModel party, ContainerViewModel palbox) : IOwnerTreeNode
    {
        public ILocalizedText Label { get; } = LocalizationCodes.LC_PLAYER_LABEL.Bind(player.Name);

        public List<IOwnerTreeNode> Children { get; } = ((List<IOwnerTreeNode>)[
            party != null ? new PlayerPartyContainerViewModel(party) : null,
            palbox != null ? new PlayerPalboxContainerViewModel(palbox) : null
        ]).SkipNull().ToList();
    }

    public class GuildTreeNodeViewModel : IOwnerTreeNode
    {
        public GuildTreeNodeViewModel(CachedSaveGame source, GuildInstance guild, List<ContainerViewModel> relevantContainers)
        {
            Label = LocalizationCodes.LC_GUILD_LABEL.Bind(guild.Name);

            var playerIds = relevantContainers.SelectMany(c => c.OwnerIds).Where(source.PlayersById.ContainsKey).Distinct().ToList();

            var playerNodes = playerIds
                .Select(pid =>
                {
                    // TODO - make use of player data to get palbox + party container IDs directly
                    var player = source.Players.Single(p => p.PlayerId == pid);
                    return new PlayerTreeNodeViewModel(
                        player: player,
                        party: relevantContainers.SingleOrDefault(c => c.Id == player.PartyContainerId),
                        palbox: relevantContainers.SingleOrDefault(c => c.Id == player.PalboxContainerId)
                    );
                })
                .Where(n => n.Children.Any())
                .ToList();

            // TODO - eventually add support for viewing cages
            var baseContainers = relevantContainers
                .Where(c => c.DetectedType == LocationType.Base)
                .OrderBy(c => c.Id)
                .Select(c => new BaseTreeNodeViewModel(c))
                .ToList();

            Children = playerNodes.Cast<IOwnerTreeNode>().Concat(baseContainers.Cast<IOwnerTreeNode>()).ToList();
        }

        public ILocalizedText Label { get; }
        public List<IOwnerTreeNode> Children { get; }
    }

    public partial class OwnerTreeViewModel : ObservableObject
    {
        public OwnerTreeViewModel(CachedSaveGame source, List<ContainerViewModel> containers)
        {
            RootNodes = containers
                .GroupBy(c =>
                {
                    var apparentGuildId = c.OwnerIds.Where(id => source.Guilds.Any(g => g.Id == id)).MostCommonOrDefault();
                    if (apparentGuildId == null)
                    {
                        apparentGuildId = c.OwnerIds.Select(id => source.GuildsByPlayerId[id].Id).MostCommonOrDefault();
                    }

                    return apparentGuildId;
                })
                .Select(group =>
                {
                    var guild = source.Guilds.Single(g => g.Id == group.Key);
                    return new GuildTreeNodeViewModel(source, guild, group.ToList());
                })
                .Cast<IOwnerTreeNode>()
                .ToList();
        }

        [NotifyPropertyChangedFor(nameof(SelectedSource))]
        [NotifyPropertyChangedFor(nameof(HasValidSource))]
        [ObservableProperty]
        private IOwnerTreeNode selectedNode;

        public IContainerSource SelectedSource => SelectedNode as IContainerSource;
        public bool HasValidSource => SelectedSource != null;

        public List<IOwnerTreeNode> RootNodes { get; }

        public IEnumerable<IContainerSource> AllContainerSources => RootNodes.SelectMany(n => n.AllChildren).Where(n => n is IContainerSource).Cast<IContainerSource>();
    }
}
