using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.Solver.Tree;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public interface IBreedingTreeNodeViewModel
    {
        IBreedingTreeNode Value { get; }

        bool IsCheckable { get; }
        bool IsChecked { get; set; }
        event Action IsCheckedChanged;
        IRelayCommand ToggleCheckedCommand { get; }

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
