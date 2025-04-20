using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.Solver
{
    public interface IPalSourceTreeNode : INotifyPropertyChanged
    {
        public ILocalizedText Label { get; }

        public bool? IsChecked { get; set; }

        public IEnumerable<IPalSourceTreeNode> Children { get; }

        /// <summary>
        /// Returns the current state of this node as a selection, which will also encompass any child-node selections.
        /// </summary>
        public List<IPalSourceTreeSelection> AsSelection { get; }

        /// <summary>
        /// Updates the state of this node, and its children, so it matches the given selections.
        /// </summary>
        public void ReadFromSelections(List<IPalSourceTreeSelection> selections);
    }

    
    public partial class PlayerSourceTreeNodeViewModel(PlayerInstance player) : ObservableObject, IPalSourceTreeNode
    {
        public PlayerInstance ModelObject => player;

        public ILocalizedText Label { get; } = new HardCodedText(player.Name);

        public IEnumerable<IPalSourceTreeNode> Children => [];

        [ObservableProperty]
        private bool? isChecked = true;

        public List<IPalSourceTreeSelection> AsSelection => IsChecked == true ? [new SourceTreePlayerSelection(player)] : [];

        public void ReadFromSelections(List<IPalSourceTreeSelection> selections)
        {
            var directSelection = selections.OfType<SourceTreePlayerSelection>().Any(s => s.ModelObject.PlayerId == player.PlayerId);
            var allItemsSelection = selections.OfType<SourceTreeAllSelection>().Any();

            IsChecked = directSelection || allItemsSelection;
        }
    }


    public partial class GuildSourceTreeNodeViewModel : ObservableObject, IPalSourceTreeNode
    {
        private int suppressSelectionCount = 0;
        private void SuppressSelectionChangedDuring(Action fn)
        {
            suppressSelectionCount++;
            try { fn(); }
            finally { --suppressSelectionCount; }
        }

        public GuildSourceTreeNodeViewModel(CachedSaveGame source, GuildInstance guild)
        {
            ModelObject = guild;
            Label = new HardCodedText(guild.Name);
            PlayerNodes = guild.MemberIds
                .Select(pid => new PlayerSourceTreeNodeViewModel(source.PlayersById[pid]))
                .OrderBy(n => n.ModelObject.Name)
                .ToList();

            Children = PlayerNodes.OfType<IPalSourceTreeNode>().ToList();

            foreach (var c in PlayerNodes)
            {
                PropertyChangedEventManager.AddHandler(c, MemberPlayer_CheckedPropertyChanged, nameof(c.IsChecked));
            }
        }

        private void MemberPlayer_CheckedPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SuppressSelectionChangedDuring(() =>
            {
                if (Children.All(c => c.IsChecked == true))
                    IsChecked = true;
                else if (Children.All(c => c.IsChecked == false))
                    IsChecked = false;
                else
                    IsChecked = null;
            });

            if (suppressSelectionCount == 0)
            {
                OnPropertyChanged(nameof(AsSelection));
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        public GuildInstance ModelObject { get; }

        public ILocalizedText Label { get; }

        public List<PlayerSourceTreeNodeViewModel> PlayerNodes { get; }
        public IEnumerable<IPalSourceTreeNode> Children { get; }

        private bool? isChecked = true;
        public bool? IsChecked
        {
            get => isChecked;
            set
            {
                if (SetProperty(ref isChecked, value))
                {
                    SuppressSelectionChangedDuring(() =>
                    {
                        if (value == true)
                        {
                            foreach (var c in PlayerNodes)
                                c.IsChecked = true;
                        }
                        else if (value == false)
                        {
                            foreach (var c in PlayerNodes)
                                c.IsChecked = false;
                        }
                    });

                    if (suppressSelectionCount == 0)
                    {
                        OnPropertyChanged(nameof(AsSelection));
                    }
                }
            }
        }

        public List<IPalSourceTreeSelection> AsSelection =>
            Children.All(c => c.IsChecked == true)
                ? [new SourceTreeGuildSelection(ModelObject)]
                : Children.SelectMany(c => c.AsSelection).SkipNull().ToList();

        public void ReadFromSelections(List<IPalSourceTreeSelection> selections)
        {
            SuppressSelectionChangedDuring(() =>
            {
                if (selections.OfType<SourceTreeGuildSelection>().Any(s => s.ModelObject.Id == ModelObject.Id))
                {
                    foreach (var c in PlayerNodes)
                        c.IsChecked = true;
                }
                else
                {
                    foreach (var c in Children)
                        c.ReadFromSelections(selections);
                }
            });

            OnPropertyChanged(nameof(AsSelection));
        }
    }


    public partial class PalSourceTreeViewModel : ObservableObject
    {
        private bool suppressSelectionChanged = false;
        private void SuppressSelectionChangedDuring(Action fn)
        {
            suppressSelectionChanged = true;
            try { fn(); }
            finally { suppressSelectionChanged = false; }
        }

        // for XAML designer view
        public PalSourceTreeViewModel() : this(CachedSaveGame.SampleForDesignerView)
        {

        }

        public CachedSaveGame Save { get; }

        public PalSourceTreeViewModel(CachedSaveGame save)
        {
            Save = save;

            RootNodes = save.Guilds
                .OrderBy(g => g.Name)
                .Select(g => new GuildSourceTreeNodeViewModel(save, g))
                .OfType<IPalSourceTreeNode>()
                .ToList();

            // only subscribe to changes in root nodes for raising change-events, try to avoid massive event
            // cascades/re-triggering
            //
            // assume root nodes will raise events appropriately if children change
            foreach (var node in RootNodes)
            {
                PropertyChangedEventManager.AddHandler(node, Node_SelectionPropertyChanged, nameof(node.AsSelection));
            }
        }

        private void Node_SelectionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!suppressSelectionChanged)
            {
                OnPropertyChanged(nameof(Selections));
                OnPropertyChanged(nameof(HasValidSource));
            }
        }

        public List<IPalSourceTreeSelection> Selections
        {
            get
            {
                return AllNodes.All(n => n.IsChecked == true)
                    ? [new SourceTreeAllSelection()]
                    : RootNodes.SelectMany(n => n.AsSelection).ToList();
            }
            set
            {
                SuppressSelectionChangedDuring(() =>
                {
                    if (value.OfType<SourceTreeAllSelection>().Any())
                    {
                        foreach (var node in AllNodes)
                            node.IsChecked = true;
                    }
                    else
                    {
                        foreach (var node in RootNodes)
                            node.ReadFromSelections(value);
                    }
                });
                OnPropertyChanged(nameof(Selections));
                OnPropertyChanged(nameof(HasValidSource));
            }
        }

        public bool HasValidSource => Selections.Any();

        public List<IPalSourceTreeNode> RootNodes { get; }

        private IEnumerable<IPalSourceTreeNode> AllNodes
        {
            get
            {
                IEnumerable<IPalSourceTreeNode> Enumerate(IPalSourceTreeNode node)
                {
                    yield return node;

                    foreach (var child in node.Children.SelectMany(Enumerate))
                    {
                        yield return child;
                    }
                }

                return RootNodes.SelectMany(Enumerate);
            }
        }
    }
}
