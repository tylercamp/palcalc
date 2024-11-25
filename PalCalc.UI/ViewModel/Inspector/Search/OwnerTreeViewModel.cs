using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Xbox;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Inspector.Search.Container;
using PalCalc.UI.ViewModel.Mapped;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public interface IOwnerTreeNode
    {
        ILocalizedText Label { get; }
        List<IOwnerTreeNode> Children => [];

        bool IsSelected { get; set; }

        public IEnumerable<IOwnerTreeNode> AllChildren => Children.Concat(Children.SelectMany(c => c.AllChildren));
    }

    public abstract class IContainerSource : ObservableObject, IOwnerTreeNode
    {
        public IContainerSource(ILocalizedText label, ISearchableContainerViewModel container)
        {
            Container = container;
            Label = label;
            SearchedLabel = Label;
        }

        private bool isSelected = false;
        public bool IsSelected
        {
            get => isSelected;
            set => SetProperty(ref isSelected, value);
        }

        public ISearchableContainerViewModel Container { get; }
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

    public class PlayerPalboxContainerViewModel(DefaultSearchableContainerViewModel container) :
        IContainerSource(LocalizationCodes.LC_PAL_LOC_PALBOX.Bind(), container)
    {
    }

    public class PlayerPartyContainerViewModel(DefaultSearchableContainerViewModel container) :
        IContainerSource(LocalizationCodes.LC_PAL_LOC_PARTY.Bind(), container)
    {
    }

    public class BaseAssignedPalsTreeNodeViewModel(DefaultSearchableContainerViewModel baseContainer) :
        IContainerSource(LocalizationCodes.LC_BASE_ASSIGNED_LABEL.Bind(), baseContainer)
    {
    }

    public class ViewingCageTreeNodeViewModel(DefaultSearchableContainerViewModel viewingCageContainer) :
        IContainerSource(LocalizationCodes.LC_VIEWING_CAGE_LABEL.Bind(viewingCageContainer.Id.Split('-')[0]), viewingCageContainer)
    {
    }

    public partial class BaseTreeNodeViewModel : ObservableObject, IOwnerTreeNode
    {
        public BaseTreeNodeViewModel(BaseInstance baseInst, DefaultSearchableContainerViewModel baseContainer, List<DefaultSearchableContainerViewModel> viewingCageContainers)
        {
            Label = LocalizationCodes.LC_BASE_LABEL.Bind(baseInst.Id.Split('-')[0]);
            Children = [];

            if (baseContainer != null)
                Children.Add(new BaseAssignedPalsTreeNodeViewModel(baseContainer));

            Children.AddRange(viewingCageContainers.OrderBy(c => c.Id).Select(c => new ViewingCageTreeNodeViewModel(c)));

            Coords = baseInst?.Position != null ? new MapCoordViewModel(baseInst.Position) : null;
        }

        public ILocalizedText Label { get; }

        public MapCoordViewModel Coords { get; }

        public List<IOwnerTreeNode> Children { get; }

        [ObservableProperty]
        private bool isSelected;
    }

    public partial class PlayerTreeNodeViewModel(
        PlayerInstance player,
        DefaultSearchableContainerViewModel party,
        DefaultSearchableContainerViewModel palbox
    ) : ObservableObject, IOwnerTreeNode
    {
        public ILocalizedText Label { get; } = LocalizationCodes.LC_PLAYER_LABEL.Bind(player.Name);

        public List<IOwnerTreeNode> Children { get; } = ((List<IOwnerTreeNode>)[
            party != null ? new PlayerPartyContainerViewModel(party) : null,
            palbox != null ? new PlayerPalboxContainerViewModel(palbox) : null
        ]).SkipNull().ToList();

        [ObservableProperty]
        private bool isSelected;
    }

    public partial class GuildTreeNodeViewModel : ObservableObject, IOwnerTreeNode
    {
        public GuildTreeNodeViewModel(CachedSaveGame source, GuildInstance guild, List<DefaultSearchableContainerViewModel> relevantContainers)
        {
            Label = LocalizationCodes.LC_GUILD_LABEL.Bind(guild.Name);

            var playerIds = relevantContainers.SelectMany(c => c.OwnerIds).Where(source.PlayersById.ContainsKey).Distinct().ToList();

            var playerNodes = playerIds
                .Select(pid =>
                {
                    var player = source.Players.Single(p => p.PlayerId == pid);
                    return new PlayerTreeNodeViewModel(
                        player: player,
                        party: relevantContainers.SingleOrDefault(c => c.Id == player.PartyContainerId),
                        palbox: relevantContainers.SingleOrDefault(c => c.Id == player.PalboxContainerId)
                    );
                })
                .Where(n => n.Children.Any())
                .ToList();

            var baseNodes = source.BasesByGuildId[guild.Id]
                .OrderBy(b => b.Id)
                .Select(b =>
                {
                    var baseContainer = relevantContainers.Where(r => r.DetectedType == LocationType.Base && r.Id == b.Container.Id).FirstOrDefault();
                    var cageContainers = relevantContainers.Where(r => r.DetectedType == LocationType.ViewingCage && b.ViewingCages.Any(v => v.Id == r.Id)).ToList();
                    return new BaseTreeNodeViewModel(b, baseContainer, cageContainers);
                })
                .Where(n => n.Children.Any())
                .ToList();

            Children = playerNodes.Cast<IOwnerTreeNode>().Concat(baseNodes.Cast<IOwnerTreeNode>()).ToList();
        }

        public ILocalizedText Label { get; }
        public List<IOwnerTreeNode> Children { get; }

        [ObservableProperty]
        private bool isSelected;
    }

    public partial class CustomizationsTreeNodeViewModel : ObservableObject, IOwnerTreeNode
    {
        public CustomizationsTreeNodeViewModel(List<CustomContainerTreeNodeViewModel> containers)
        {
            Label = LocalizationCodes.LC_CUSTOM_CONTAINERS.Bind();

            Children = containers
                .OrderBy(c => c.Label.Value)
                .Cast<IOwnerTreeNode>()
                .Append(new NewCustomContainerTreeNodeViewModel())
                .ToList();
        }

        public ILocalizedText Label { get; }
        public List<IOwnerTreeNode> Children { get; }

        [ObservableProperty]
        private bool isSelected;
    }

    public partial class NewCustomContainerTreeNodeViewModel : ObservableObject, IOwnerTreeNode
    {
        public ILocalizedText Label { get; } = LocalizationCodes.LC_CUSTOM_CONTAINER_ADD_NEW.Bind();

        [ObservableProperty]
        private bool isSelected;
    }

    public partial class CustomContainerTreeNodeViewModel(CustomSearchableContainerViewModel customContainer)
        : IContainerSource(new CustomContainerLocalizedText(customContainer), customContainer)
    {
        private partial class CustomContainerLocalizedText : ILocalizedText
        {
            private CustomSearchableContainerViewModel container;

            public CustomContainerLocalizedText(CustomSearchableContainerViewModel container)
            {
                this.container = container;

                PropertyChangedEventManager.AddHandler(
                    container,
                    (_, _) => OnPropertyChanged(nameof(Value)),
                    nameof(container.Label)
                );
            }

            public override string Value => container.Label;
        }

        // (needed to give ContextMenu access to rename/delete commands)
        public CustomSearchableContainerViewModel CustomContainer => Container as CustomSearchableContainerViewModel;
    }

    public partial class OwnerTreeViewModel : ObservableObject
    {
        private static ILogger logger = Log.ForContext<OwnerTreeViewModel>();

        public OwnerTreeViewModel(CachedSaveGame source, List<ISearchableContainerViewModel> containers)
        {
            var guildContainers = new Dictionary<string, List<DefaultSearchableContainerViewModel>>();

            foreach (var container in containers.OfType<DefaultSearchableContainerViewModel>())
            {
                string guildId = source.GuildsByContainerId.GetValueOrDefault(container.Id)?.Id;

                if (!guildContainers.ContainsKey(guildId))
                    guildContainers.Add(guildId, []);

                guildContainers[guildId].Add(container);
            }

            var standardNodes = source.Guilds
                .Select(guild => new GuildTreeNodeViewModel(source, guild, guildContainers.GetValueOrElse(guild.Id, [])))
                .Cast<IOwnerTreeNode>();

            var customNode = new CustomizationsTreeNodeViewModel(
                containers
                    .OfType<CustomSearchableContainerViewModel>()
                    .Select(c => new CustomContainerTreeNodeViewModel(c))
                    .ToList()
            );

            RootNodes = standardNodes.Append(customNode).ToList();
        }

        private IOwnerTreeNode selectedNode;
        public IOwnerTreeNode SelectedNode
        {
            get => selectedNode;
            set
            {
                if (SetProperty(ref selectedNode, value))
                {
                    OnPropertyChanged(nameof(SelectedSource));
                    OnPropertyChanged(nameof(HasValidSource));

                    if (selectedNode is NewCustomContainerTreeNodeViewModel)
                    {
                        Dispatcher.CurrentDispatcher.BeginInvoke(() => value.IsSelected = false);
                        CreateCustomContainerCommand?.Execute(null);
                    }
                    else if (selectedNode != null && selectedNode is not IContainerSource)
                    {
                        // if the selected node isn't a valid source, and it only has one valid child source, just select
                        // the valid child instead
                        var childSources = selectedNode.AllChildren.Where(c => c is IContainerSource).ToList();
                        if (childSources.Count == 1)
                        {
                            Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                            {
                                value.IsSelected = false;
                                childSources[0].IsSelected = true;
                            });
                        }
                    }
                }
            }
        }

        public IContainerSource SelectedSource => SelectedNode as IContainerSource;
        public bool HasValidSource => SelectedSource != null;

        public List<IOwnerTreeNode> RootNodes { get; }

        public ICommand CreateCustomContainerCommand { get; set; }

        public IEnumerable<IContainerSource> AllContainerSources =>
            RootNodes.SelectMany(n => n.AllChildren).OfType<IContainerSource>();
    }
}
