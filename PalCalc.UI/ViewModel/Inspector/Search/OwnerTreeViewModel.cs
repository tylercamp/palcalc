using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
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
        string Label { get; }
        List<IOwnerTreeNode> Children => [];

        public IEnumerable<IOwnerTreeNode> AllChildren => Children.Concat(Children.SelectMany(c => c.AllChildren));
    }

    public interface IContainerSource : IOwnerTreeNode
    {
        ContainerViewModel Container { get; }

        ISearchCriteria SearchCriteria { set; }
    }

    public static class ContainerSourceExtensions
    {
        public static string ToSearchResultsLabel(this IContainerSource src) =>
            $"{src.Label} ({src.Container.Grids.Sum(g => g.Slots.Count(s => s.Matches))} matches)";
    }

    public class PlayerPalboxContainerViewModel(ContainerViewModel container) : ObservableObject, IContainerSource
    {
        public string Label => "Palbox";
        public ContainerViewModel Container => container;

        public string SearchedLabel => this.ToSearchResultsLabel();
        public ISearchCriteria SearchCriteria
        {
            set
            {
                Container.SearchCriteria = value;
                OnPropertyChanged(nameof(SearchedLabel));
            }
        }
    }

    public class PlayerPartyContainerViewModel(ContainerViewModel container) : ObservableObject, IContainerSource
    {
        public string Label => "Party";
        public ContainerViewModel Container => container;

        public string SearchedLabel => this.ToSearchResultsLabel();
        public ISearchCriteria SearchCriteria
        {
            set
            {
                Container.SearchCriteria = value;
                OnPropertyChanged(nameof(SearchedLabel));
            }
        }
    }

    public class PlayerTreeNodeViewModel(PlayerInstance player, ContainerViewModel party, ContainerViewModel palbox) : IOwnerTreeNode
    {
        public string Label => $"Player '{player.Name}'";

        public List<IOwnerTreeNode> Children { get; } = [
            new PlayerPartyContainerViewModel(party),
            new PlayerPalboxContainerViewModel(palbox)
        ];
    }

    public class BaseTreeNodeViewModel(ContainerViewModel baseContainer) : ObservableObject, IContainerSource
    {
        public string Label => $"Base ({baseContainer.Id.Split('-')[0]})";
        public ContainerViewModel Container => baseContainer;

        public string SearchedLabel => this.ToSearchResultsLabel();
        public ISearchCriteria SearchCriteria
        {
            set
            {
                Container.SearchCriteria = value;
                OnPropertyChanged(nameof(SearchedLabel));
            }
        }
    }

    public class GuildTreeNodeViewModel : IOwnerTreeNode
    {
        public GuildTreeNodeViewModel(CachedSaveGame source, GuildInstance guild, List<ContainerViewModel> relevantContainers)
        {
            Label = $"Guild '{guild.Name}'";

            var playerIds = relevantContainers.SelectMany(c => c.OwnerIds).Where(source.PlayersById.ContainsKey).Distinct().ToList();

            var playerNodes = playerIds
                .Select(pid =>
                {
                    // TODO - make use of player data to get palbox + party container IDs directly
                    var player = source.Players.Single(p => p.PlayerId == pid);
                    return new PlayerTreeNodeViewModel(
                        player: player,
                        party: relevantContainers.Single(c => c.DetectedType == LocationType.PlayerParty && c.OwnerIds.Count == 1 && c.OwnerIds.Single() == pid),
                        palbox: relevantContainers.Single(c => c.DetectedType == LocationType.Palbox && c.OwnerIds.Count == 1 && c.OwnerIds.Single() == pid)
                    );
                })
                .ToList();

            // TODO - eventually add support for viewing cages
            var baseContainers = relevantContainers
                .Where(c => c.DetectedType == LocationType.Base)
                .OrderBy(c => c.Id)
                .Select(c => new BaseTreeNodeViewModel(c))
                .ToList();

            Children = playerNodes.Cast<IOwnerTreeNode>().Concat(baseContainers.Cast<IOwnerTreeNode>()).ToList();
        }

        public string Label { get; }
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
                        apparentGuildId = c.OwnerIds.Select(id => source.GuildsByPlayerId[id]).MostCommonOrDefault().Id;
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
