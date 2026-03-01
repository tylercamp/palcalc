using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver.Tree;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public interface IBreedingTreeNodeViewModel : INotifyPropertyChanged
    {
        IBreedingTreeNode Value { get; }

        bool IsCheckable { get; }
        bool IsChecked { get; set; }
        bool IsComplete { get; }
        event Action IsCheckedChanged;
        IRelayCommand ToggleCheckedCommand { get; }

        /// <summary>
        /// Sets the "consumer" node — the node that this one feeds into (closer to the final result).
        /// When the consumer becomes complete, this node also becomes complete.
        /// </summary>
        void SetConsumer(IBreedingTreeNodeViewModel consumer);

        public static IBreedingTreeNodeViewModel FromModel(CachedSaveGame source, GameSettings settings, IBreedingTreeNode node) =>
            node switch
            {
                SurgeryOperationNode spn => new SurgeryBreedingTreeNodeViewModel(spn),
                _ => new StandardBreedingTreeNodeViewModel(source, settings, node)
            };
    }

    public interface IRefreshableNode
    {
        bool NeedsRefresh { get; }
    }
}
