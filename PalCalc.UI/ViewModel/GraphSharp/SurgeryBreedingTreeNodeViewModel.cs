using PalCalc.Solver.PalReference;
using PalCalc.Solver.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public class SurgeryOperationViewModel(ISurgeryOperation op)
    {
        public string Description => op.ToString(); // TODO - itl
    }

    // (note: this node just represents the breeding table itself, the result pal is a separate (parent) node)
    public partial class SurgeryBreedingTreeNodeViewModel : IBreedingTreeNodeViewModel
    {
        public SurgeryBreedingTreeNodeViewModel(SurgeryOperationNode node)
        {
            pref = node.PalRef as SurgeryTablePalReference;

            Value = node;
            Operations = pref.Operations.Select(op => new SurgeryOperationViewModel(op)).ToList();
            GoldCost = pref.Operations.Sum(op => op.GoldCost);
        }

        private SurgeryTablePalReference pref;

        public IBreedingTreeNode Value { get; }

        public List<SurgeryOperationViewModel> Operations { get; }

        public int GoldCost { get; }
    }
}
